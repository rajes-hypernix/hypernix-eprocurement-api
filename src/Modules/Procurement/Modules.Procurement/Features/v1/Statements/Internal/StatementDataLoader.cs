using FSH.Modules.Procurement.Data;
using FSH.Modules.Suppliers.Contracts.Services;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Statements.Internal;

internal static class StatementDataLoader
{
    public static async Task<SoaCalculator> LoadAsync(
        ProcurementDbContext dbContext,
        IVendorLookupService vendorLookup,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? extraVendorIds = null)
    {
        var pos = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grns = await dbContext.Grns
            .AsNoTracking()
            .Include(g => g.Lines)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var unnamed = new SoaCalculator(pos, grns, invoices, DateTime.UtcNow, new Dictionary<Guid, string>());
        var ids = unnamed.VendorIds.Concat(extraVendorIds ?? []).Distinct().ToList();
        var vendors = await vendorLookup.GetManyAsync(ids, cancellationToken).ConfigureAwait(false);
        var names = vendors.ToDictionary(v => v.Key, v => v.Value.Name);

        return new SoaCalculator(pos, grns, invoices, DateTime.UtcNow, names);
    }
}
