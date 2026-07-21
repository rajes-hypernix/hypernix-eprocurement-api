using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Platform.Contracts.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Platform.Data;

public sealed class PlatformDbInitializer(
    PlatformDbContext dbContext,
    IRoleService roleService,
    ILogger<PlatformDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Platform] applied migrations");
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        PlatformLookupSeedData.Seed(dbContext);
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Platform] seeded lookup reference data");
        }

        await MergeRolePermissionsAsync(
            "Buyer",
            "Buyer — creates requisitions/RFQs and purchase orders, receives goods, approves invoices.",
            [
                PlatformPermissions.Lookups.View,
                PlatformPermissions.Lookups.Manage,
                PlatformPermissions.CustomLists.View,
                PlatformPermissions.CustomLists.Manage,
                PlatformPermissions.Org.View,
                PlatformPermissions.Org.Manage,
                PlatformPermissions.FormTemplates.View,
                PlatformPermissions.FormTemplates.Manage,
            ],
            cancellationToken).ConfigureAwait(false);

        await MergeRolePermissionsAsync(
            "Vendor",
            "Vendor — onboarding, bidding, PO acknowledge, ASN, invoice submit.",
            [
                PlatformPermissions.Lookups.View,
                PlatformPermissions.CustomLists.View,
                PlatformPermissions.FormTemplates.View,
            ],
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
                logger.LogInformation("[Platform] seeded {Role} role", roleName);
        }

        var withPerms = await roleService.GetWithPermissionsAsync(role.Id, cancellationToken).ConfigureAwait(false);
        var merged = (withPerms.Permissions ?? [])
            .Concat(permissionsToAdd)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await roleService.UpdatePermissionsAsync(role.Id, merged, cancellationToken).ConfigureAwait(false);
    }
}
