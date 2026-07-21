using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.DTOs;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Sourcing.Data;

public sealed class SourcingDbInitializer(
    SourcingDbContext dbContext,
    IRoleService roleService,
    ILogger<SourcingDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Sourcing] applied migrations");
        }
    }

    /// <summary>
    /// Seeds the old system's Buyer/Approver/TechEvaluator/CommEvaluator role/action matrix as
    /// real FSH roles (Admin already covers everything; Vendor is seeded by Suppliers). None of
    /// these are <see cref="FSH.Framework.Shared.Constants.RoleConstants.DefaultRoles"/>, so they
    /// get zero permissions automatically — every grant below is explicit. Idempotent: permission
    /// lists are pushed via <see cref="IRoleService.UpdatePermissionsAsync"/>, which reconciles
    /// (adds missing, removes stale) rather than only inserting, so a later matrix change here
    /// self-heals on the next seed run.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await SeedRoleAsync(
            "Buyer",
            "Buyer — creates requisitions, manages RFQs, submits awards.",
            [
                SourcingPermissions.Requisitions.View,
                SourcingPermissions.Requisitions.Manage,
                SourcingPermissions.Rfqs.View,
                SourcingPermissions.Rfqs.ManageDraft,
                SourcingPermissions.Rfqs.ManageLifecycle,
                SourcingPermissions.Rfqs.Invite,
                SourcingPermissions.Rfqs.Rescind,
                SourcingPermissions.Rfqs.Extend,
                SourcingPermissions.Evaluation.ViewOpening,
                SourcingPermissions.Evaluation.ViewTechnical,
                SourcingPermissions.Award.View,
                SourcingPermissions.Award.Submit,
                SourcingPermissions.Clarifications.View,
                SourcingPermissions.Clarifications.Send,
            ],
            cancellationToken).ConfigureAwait(false);

        await SeedRoleAsync(
            "Approver",
            "Approver — Delegation-of-Authority sign-off on awards.",
            [
                SourcingPermissions.Award.View,
                SourcingPermissions.Award.Approve,
            ],
            cancellationToken).ConfigureAwait(false);

        await SeedRoleAsync(
            "TechEvaluator",
            "Technical evaluator — scores sealed bids, blind to vendor identity.",
            [
                SourcingPermissions.Evaluation.OpenTechnical,
                SourcingPermissions.Evaluation.Score,
                SourcingPermissions.Evaluation.FinalizeTechnical,
                SourcingPermissions.Evaluation.ViewTechnical,
            ],
            cancellationToken).ConfigureAwait(false);

        await SeedRoleAsync(
            "CommEvaluator",
            "Commercial evaluator — opens the sealed commercial envelope.",
            [
                SourcingPermissions.Evaluation.OpenCommercial,
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedRoleAsync(string roleName, string description, List<string> permissions, CancellationToken cancellationToken)
    {
        var existing = await roleService.GetRolesAsync(1, 1, roleName, cancellationToken).ConfigureAwait(false);
        var role = existing.Items.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));
        if (role is null)
        {
            role = await roleService.CreateOrUpdateRoleAsync(string.Empty, roleName, description, cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[Sourcing] seeded {Role} role", roleName);
            }
        }

        await roleService.UpdatePermissionsAsync(role.Id, permissions, cancellationToken).ConfigureAwait(false);
    }
}
