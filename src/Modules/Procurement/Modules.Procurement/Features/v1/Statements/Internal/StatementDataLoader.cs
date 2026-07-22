using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Statements.Internal;

internal static class StatementDataLoader
{
    public static async Task<SoaCalculator> LoadAsync(ProcurementDbContext dbContext, CancellationToken cancellationToken)
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

        return new SoaCalculator(pos, grns, invoices, DateTime.UtcNow);
    }
}
