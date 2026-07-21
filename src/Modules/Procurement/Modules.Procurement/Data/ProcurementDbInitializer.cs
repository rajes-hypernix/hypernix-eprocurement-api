using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Procurement.Contracts.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Procurement.Data;

public sealed class ProcurementDbInitializer(
    ProcurementDbContext dbContext,
    IRoleService roleService,
    ILogger<ProcurementDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Procurement] applied migrations");
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        // Merge onto existing Buyer/Vendor grants (Sourcing/Suppliers seed first).
        // UpdatePermissionsAsync replaces the full set, so we must union rather than overwrite.
        await MergeRolePermissionsAsync(
            "Buyer",
            "Buyer — creates requisitions/RFQs and purchase orders, receives goods, approves invoices.",
            [
                ProcurementPermissions.PurchaseOrders.View,
                ProcurementPermissions.PurchaseOrders.CreateFromAward,
                ProcurementPermissions.PurchaseOrders.Issue,
                ProcurementPermissions.Deliveries.View,
                ProcurementPermissions.Deliveries.Receive,
                ProcurementPermissions.Invoices.View,
                ProcurementPermissions.Invoices.Approve,
                ProcurementPermissions.Invoices.ResolveException,
            ],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "Vendor",
            "Vendor — onboarding, bidding, PO acknowledge, ASN, invoice submit.",
            [
                ProcurementPermissions.PurchaseOrders.View,
                ProcurementPermissions.PurchaseOrders.Acknowledge,
                ProcurementPermissions.Deliveries.View,
                ProcurementPermissions.Deliveries.CreateAsn,
                ProcurementPermissions.Invoices.View,
                ProcurementPermissions.Invoices.Submit,
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task MergeRolePermissionsAsync(
        string roleName,
        string description,
        List<string> permissionsToAdd,
        CancellationToken cancellationToken)
    {
        // Avoid GetRolesAsync(search, pageSize:1) — Contains + Take(1) can miss the exact role.
        var catalog = await roleService.GetRolesAsync(1, 100, search: null, cancellationToken).ConfigureAwait(false);
        var role = catalog.Items.FirstOrDefault(r => string.Equals(r.Name, roleName, StringComparison.Ordinal));
        if (role is null)
        {
            role = await roleService.CreateOrUpdateRoleAsync(string.Empty, roleName, description, cancellationToken)
                .ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("[Procurement] seeded {Role} role", roleName);
            }
        }

        var withPerms = await roleService.GetWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var merged = (withPerms.Permissions ?? [])
            .Concat(permissionsToAdd)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await roleService.UpdatePermissionsAsync(role.Id, merged, cancellationToken).ConfigureAwait(false);
    }
}
