using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.Ordering;

/// <summary>
/// IC14/IC15: the row-lock + cap-check + ledger-insert all happen inside this one Sourcing-local
/// transaction, so concurrent direct-order requests against the same requisition can't both pass.
/// </summary>
public sealed class ReserveRequisitionQuantityCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<ReserveRequisitionQuantityCommand, IReadOnlyList<Guid>>
{
    public async ValueTask<IReadOnlyList<Guid>> Handle(ReserveRequisitionQuantityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new SourcingRuleException("At least one line is required to reserve quantity.");
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"sourcing\".\"PurchaseRequisitions\" WHERE \"Id\" = {command.PrId} FOR UPDATE",
            cancellationToken).ConfigureAwait(false);

        var pr = await dbContext.PurchaseRequisitions
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PrId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {command.PrId} not found.");

        var prLineIds = pr.Lines.Select(l => l.Id).ToList();
        var orderedByLine = await dbContext.PrLineOrders.AsNoTracking()
            .Where(o => o.LinkStatus == LinkStatus.Active && prLineIds.Contains(o.PrLineId))
            .GroupBy(o => o.PrLineId)
            .Select(g => new { PrLineId = g.Key, Qty = g.Sum(x => x.QtyOrdered) })
            .ToDictionaryAsync(x => x.PrLineId, x => x.Qty, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var reservations = new List<PrLineOrder>();
        foreach (var input in command.Lines)
        {
            var line = pr.GetLine(input.PrLineId);
            if (line.LifecycleStatus != PrLineStatus.Open)
            {
                throw new SourcingRuleException(
                    $"Line {line.ItemCode} is {line.LifecycleStatus} — only an Open line can be ordered.");
            }

            var alreadyOrdered = orderedByLine.GetValueOrDefault(input.PrLineId);
            var remaining = line.Qty - alreadyOrdered;
            if (input.Qty <= 0 || input.Qty > remaining)
            {
                throw new SourcingRuleException(
                    $"Quantity {input.Qty} for {line.ItemCode} must be positive and at most the remaining " +
                    $"{remaining} (of {line.Qty} requisitioned; {alreadyOrdered} already ordered).");
            }

            reservations.Add(PrLineOrder.Create(input.PrLineId, command.PoId, null, input.Qty, now));
            orderedByLine[input.PrLineId] = alreadyOrdered + input.Qty;
        }

        dbContext.PrLineOrders.AddRange(reservations);
        pr.RecomputeHeaderStatus(orderedByLine);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        return reservations.Select(r => r.Id).ToList();
    }
}
