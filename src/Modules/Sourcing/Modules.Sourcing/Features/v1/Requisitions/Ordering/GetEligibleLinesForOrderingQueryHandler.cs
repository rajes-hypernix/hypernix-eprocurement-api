using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.Ordering;

/// <summary>
/// Mirrors the old source's PoBuilderService.EligibleLinesAsync/Classify exactly: PR headers
/// Submitted or PartiallySourced only (ported as-is, including that a fully PartiallyOrdered/
/// Ordered header is not re-included here — matches the source, not a gap introduced by this port).
/// </summary>
public sealed class GetEligibleLinesForOrderingQueryHandler(SourcingDbContext dbContext)
    : IQueryHandler<GetEligibleLinesForOrderingQuery, IReadOnlyList<EligibleOrderLineDto>>
{
    public async ValueTask<IReadOnlyList<EligibleOrderLineDto>> Handle(
        GetEligibleLinesForOrderingQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var prs = await dbContext.PurchaseRequisitions
            .AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.HeaderStatus == PrHeaderStatus.Submitted || p.HeaderStatus == PrHeaderStatus.PartiallySourced)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (prs.Count == 0)
        {
            return [];
        }

        var lineIds = prs.SelectMany(p => p.Lines).Select(l => l.Id).ToList();
        var orderedByLine = await dbContext.PrLineOrders
            .AsNoTracking()
            .Where(o => o.LinkStatus == LinkStatus.Active && lineIds.Contains(o.PrLineId))
            .GroupBy(o => o.PrLineId)
            .Select(g => new { PrLineId = g.Key, Qty = g.Sum(x => x.QtyOrdered) })
            .ToDictionaryAsync(x => x.PrLineId, x => x.Qty, cancellationToken)
            .ConfigureAwait(false);

        var result = new List<EligibleOrderLineDto>();
        foreach (var pr in prs)
        {
            foreach (var line in pr.Lines)
            {
                var (include, sourceable, reason) = Classify(line);
                if (!include)
                {
                    continue;
                }

                var ordered = orderedByLine.GetValueOrDefault(line.Id);
                result.Add(new EligibleOrderLineDto(
                    pr.Id, pr.Code, line.Id, line.ItemCode, line.Description, line.Qty, line.Uom,
                    line.EstUnitPrice, ordered, line.Qty - ordered, sourceable, reason));
            }
        }

        return result;
    }

    private static (bool Include, bool Sourceable, string? Reason) Classify(PrLine line) => line.LifecycleStatus switch
    {
        PrLineStatus.Open => (true, true, null),
        PrLineStatus.InDraftRfq => (true, false, "In a draft basket/RFQ"),
        PrLineStatus.InRfq => (true, false, line.Ref is { Length: > 0 } ? $"In RFQ {line.Ref}" : "In an RFQ"),
        PrLineStatus.Awarded => (true, false, "Awarded"),
        _ => (false, false, null),
    };
}
