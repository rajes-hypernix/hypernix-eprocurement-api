using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.Ordering;

/// <summary>
/// Compensating release: closes every active <see cref="PrLineOrder"/> reservation tied to the
/// given PO (its own Cancel, or a rollback after a failed PO save) and recomputes the header
/// status of every requisition that had a line touched.
/// </summary>
public sealed class ReleaseRequisitionQuantityCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<ReleaseRequisitionQuantityCommand>
{
    public async ValueTask<Unit> Handle(ReleaseRequisitionQuantityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reservations = await dbContext.PrLineOrders
            .Where(o => o.PoId == command.PoId && o.LinkStatus == LinkStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (reservations.Count == 0)
        {
            return Unit.Value;
        }

        var now = DateTime.UtcNow;
        foreach (var reservation in reservations)
        {
            reservation.MarkReleased(command.Reason, now);
        }

        var prLineIds = reservations.Select(r => r.PrLineId).Distinct().ToList();
        var purchaseRequisitions = await dbContext.PurchaseRequisitions
            .Include(p => p.Lines)
            .Where(p => p.Lines.Any(l => prLineIds.Contains(l.Id)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var pr in purchaseRequisitions)
        {
            var linesOfThisPr = pr.Lines.Select(l => l.Id).ToHashSet();

            // Excludes the reservations we just closed (same PoId) rather than re-querying post-save,
            // since SaveChangesAsync hasn't run yet — the DB still shows them as Active at this point.
            var stillActive = await dbContext.PrLineOrders.AsNoTracking()
                .Where(o => o.LinkStatus == LinkStatus.Active && o.PoId != command.PoId && linesOfThisPr.Contains(o.PrLineId))
                .GroupBy(o => o.PrLineId)
                .Select(g => new { PrLineId = g.Key, Qty = g.Sum(x => x.QtyOrdered) })
                .ToDictionaryAsync(x => x.PrLineId, x => x.Qty, cancellationToken)
                .ConfigureAwait(false);

            pr.RecomputeHeaderStatus(stillActive);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
