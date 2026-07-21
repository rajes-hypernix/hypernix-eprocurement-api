using FSH.Framework.Persistence;
using FSH.Modules.Identity.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Suppliers.Data;

public sealed class SuppliersDbInitializer(
    SuppliersDbContext dbContext,
    IRoleService roleService,
    ILogger<SuppliersDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Suppliers] applied migrations");
        }
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.SwecCategories.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            dbContext.SwecCategories.AddRange(SwecTaxonomySeedData.Build());
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[Suppliers] seeded SWEC taxonomy");
        }

        await SeedVendorRoleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Vendor-portal logins get a non-default "Vendor" role. Domain endpoints scope by VendorId
    /// claim; inbox/platform/procurement permissions are merged onto this role by later modules.
    /// Idempotent: a matching role name is left untouched.
    /// </summary>
    private async Task SeedVendorRoleAsync(CancellationToken cancellationToken)
    {
        const string vendorRoleName = "Vendor";
        var existing = await roleService.GetRolesAsync(1, 1, vendorRoleName, cancellationToken).ConfigureAwait(false);
        if (existing.Items.Any(r => string.Equals(r.Name, vendorRoleName, StringComparison.Ordinal)))
        {
            return;
        }

        await roleService.CreateOrUpdateRoleAsync(
            string.Empty,
            vendorRoleName,
            "Vendor-portal login — scoped by VendorId claim, not by permission.",
            cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[Suppliers] seeded Vendor role");
    }
}
