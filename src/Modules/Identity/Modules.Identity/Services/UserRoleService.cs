using System.Net;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Identity.Data;
using FSH.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Identity.Services;

internal sealed class UserRoleService(
    UserManager<FshUser> userManager,
    RoleManager<FshRole> roleManager,
    IdentityDbContext db,
    IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
    ICurrentUser currentUser,
    IUserPermissionService userPermissionService,
    IAuditClient auditClient) : IUserRoleService
{
    public async Task<string> AssignRolesAsync(string userId, List<UserRoleDto> userRoles, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("user not found");

        await ValidateAdminRoleChangeAsync(user, userRoles);

        var (addedRoles, removedRoles) = await ProcessRoleAssignmentsAsync(user, userRoles);

        await RaiseRolesAssignedEventAsync(user, addedRoles, cancellationToken);
        await AuditRoleChangesAsync(userId, addedRoles, removedRoles, cancellationToken).ConfigureAwait(false);

        // Any role mutation (add or remove) invalidates the cached permission set; flush
        // unconditionally rather than gating on addedRoles, which only tracks additions.
        await userPermissionService.InvalidatePermissionCacheAsync(userId, cancellationToken).ConfigureAwait(false);

        return "User Roles Updated Successfully.";
    }

    public async Task<List<UserRoleDto>> GetUserRolesAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException("user not found");

        var roles = await roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken)
            ?? throw new NotFoundException("roles not found");

        // Single membership query instead of one IsInRoleAsync round-trip per role.
        var memberships = await userManager.GetRolesAsync(user);
        var membershipSet = new HashSet<string>(memberships, StringComparer.OrdinalIgnoreCase);

        var userRoles = new List<UserRoleDto>();
        foreach (var role in roles)
        {
            userRoles.Add(new UserRoleDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Enabled = membershipSet.Contains(role.Name!)
            });
        }

        return userRoles;
    }

    private async Task ValidateAdminRoleChangeAsync(FshUser user, List<UserRoleDto> userRoles)
    {
        bool isRemovingAdminRole = userRoles.Exists(a => !a.Enabled && a.RoleName == RoleConstants.Admin);
        if (!isRemovingAdminRole)
        {
            return;
        }

        bool userIsAdmin = await userManager.IsInRoleAsync(user, RoleConstants.Admin);
        if (!userIsAdmin)
        {
            return;
        }

        // Administrators cannot demote themselves — they would lose access immediately on the next request,
        // and would need another admin to restore them.
        var actorId = currentUser.GetUserId();
        if (actorId != Guid.Empty && string.Equals(actorId.ToString(), user.Id, StringComparison.Ordinal))
        {
            throw new CustomException(
                "Administrators cannot remove their own admin role.",
                Array.Empty<string>(),
                HttpStatusCode.BadRequest);
        }

        // The root tenant's seed admin is the framework's last-resort recovery account.
        if (IsRootTenantAdmin(user))
        {
            throw new ForbiddenException("The root tenant administrator cannot be demoted.");
        }

        // After this removal, at least one admin must remain in the tenant — matches
        // the "at least one active administrator" invariant enforced on user deactivation.
        await EnsureMinimumAdminCountAsync();
    }

    private bool IsRootTenantAdmin(FshUser user)
    {
        return user.Email == MultitenancyConstants.Root.EmailAddress
            && multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id == MultitenancyConstants.Root.Id;
    }

    private async Task EnsureMinimumAdminCountAsync()
    {
        int adminCount = (await userManager.GetUsersInRoleAsync(RoleConstants.Admin)).Count;
        if (adminCount <= 1)
        {
            throw new CustomException(
                "Tenant must retain at least one administrator.",
                Array.Empty<string>(),
                HttpStatusCode.BadRequest);
        }
    }

    private async Task<(List<string> Added, List<string> Removed)> ProcessRoleAssignmentsAsync(
        FshUser user,
        List<UserRoleDto> userRoles)
    {
        var addedRoles = new List<string>();
        var removedRoles = new List<string>();

        foreach (var userRole in userRoles)
        {
            if (await roleManager.FindByNameAsync(userRole.RoleName!) is null)
            {
                continue;
            }

            var roleName = userRole.RoleName!;
            var inRole = await userManager.IsInRoleAsync(user, roleName);

            if (userRole.Enabled)
            {
                if (!inRole)
                {
                    await userManager.AddToRoleAsync(user, roleName);
                    addedRoles.Add(roleName);
                }
            }
            else if (inRole)
            {
                await userManager.RemoveFromRoleAsync(user, roleName);
                removedRoles.Add(roleName);
            }
        }

        return (addedRoles, removedRoles);
    }

    private async Task RaiseRolesAssignedEventAsync(FshUser user, List<string> assignedRoles, CancellationToken cancellationToken)
    {
        if (assignedRoles.Count == 0)
        {
            return;
        }

        var tenantId = multiTenantContextAccessor?.MultiTenantContext?.TenantInfo?.Id;
        user.RecordRolesAssigned(assignedRoles, tenantId);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes a friendly EntityChange against the user key so History shows role adds/removes
    /// (AspNet UserRoles join rows are PK-only and are skipped by the EF change interceptor).
    /// </summary>
    private async Task AuditRoleChangesAsync(
        string userId,
        List<string> addedRoles,
        List<string> removedRoles,
        CancellationToken cancellationToken)
    {
        if (addedRoles.Count == 0 && removedRoles.Count == 0)
        {
            return;
        }

        var changes = new List<PropertyChange>();
        if (addedRoles.Count > 0)
        {
            changes.Add(new PropertyChange(
                Name: "RolesAdded",
                DataType: "string",
                OldValue: null,
                NewValue: string.Join(", ", addedRoles),
                IsSensitive: false));
        }

        if (removedRoles.Count > 0)
        {
            changes.Add(new PropertyChange(
                Name: "RolesRemoved",
                DataType: "string",
                OldValue: string.Join(", ", removedRoles),
                NewValue: null,
                IsSensitive: false));
        }

        await auditClient.WriteEntityChangeAsync(
            dbContext: nameof(IdentityDbContext),
            schema: IdentityModuleConstants.SchemaName,
            table: "UserRoles",
            entityName: "UserRole",
            key: $"Id:{userId}",
            operation: EntityOperation.Update,
            changes: changes,
            source: "Identity",
            ct: cancellationToken).ConfigureAwait(false);

        if (addedRoles.Count > 0)
        {
            await auditClient.WriteSecurityAsync(
                SecurityAction.RoleAssigned,
                subjectId: userId,
                claims: new Dictionary<string, object?>
                {
                    ["targetUserId"] = userId,
                    ["roles"] = addedRoles.ToArray(),
                },
                source: "Identity",
                ct: cancellationToken).ConfigureAwait(false);
        }

        if (removedRoles.Count > 0)
        {
            await auditClient.WriteSecurityAsync(
                SecurityAction.RoleRevoked,
                subjectId: userId,
                claims: new Dictionary<string, object?>
                {
                    ["targetUserId"] = userId,
                    ["roles"] = removedRoles.ToArray(),
                },
                source: "Identity",
                ct: cancellationToken).ConfigureAwait(false);
        }
    }
}