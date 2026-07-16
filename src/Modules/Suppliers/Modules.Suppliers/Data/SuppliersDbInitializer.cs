using FSH.Framework.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Suppliers.Data;

public sealed class SuppliersDbInitializer(
    SuppliersDbContext dbContext,
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
    }
}
