using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Notifications.Contracts.Authorization;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Communication.Data;

/// <summary>
/// No Communication schema — only merges inbox permissions onto Buyer/Vendor so they can
/// read notifications written by eProcure integration handlers (Vendor is not Basic).
/// </summary>
public sealed class CommunicationDbInitializer(
    IRoleService roleService,
    ILogger<CommunicationDbInitializer> logger) : IDbInitializer
{
    public Task MigrateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await MergeRolePermissionsAsync(
            "Buyer",
            "Buyer — creates requisitions/RFQs and purchase orders, receives goods, approves invoices.",
            [NotificationPermissions.Inbox.View, NotificationPermissions.Inbox.MarkRead],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "Vendor",
            "Vendor — onboarding, bidding, PO acknowledge, ASN, invoice submit.",
            [NotificationPermissions.Inbox.View, NotificationPermissions.Inbox.MarkRead],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task MergeRolePermissionsAsync(
        string roleName,
        string description,
        List<string> permissionsToAdd,
        CancellationToken cancellationToken)
    {
        var catalog = await roleService.GetRolesAsync(1, 100, search: null, cancellationToken).ConfigureAwait(false);
        var role = catalog.Items.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));
        if (role is null)
        {
            role = await roleService.CreateOrUpdateRoleAsync(string.Empty, roleName, description, cancellationToken)
                .ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[Communication] seeded {Role} role", roleName);
        }

        var withPerms = await roleService.GetWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var merged = (withPerms.Permissions ?? [])
            .Concat(permissionsToAdd)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await roleService.UpdatePermissionsAsync(role.Id, merged, cancellationToken).ConfigureAwait(false);
    }
}
