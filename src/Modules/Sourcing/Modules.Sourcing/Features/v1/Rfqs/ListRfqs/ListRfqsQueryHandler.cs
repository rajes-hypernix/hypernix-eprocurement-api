using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ListRfqs;

public sealed class ListRfqsQueryHandler(SourcingDbContext dbContext)
    : IQueryHandler<ListRfqsQuery, IReadOnlyList<RfqListItemDto>>
{
    public async ValueTask<IReadOnlyList<RfqListItemDto>> Handle(ListRfqsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfqs = await dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rfqIds = rfqs.Select(r => r.Id).ToList();
        var bidCountsByRfq = await dbContext.Bids
            .AsNoTracking()
            .Where(b => rfqIds.Contains(b.RfqId))
            .GroupBy(b => b.RfqId)
            .Select(g => new { RfqId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RfqId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rfqs.Select(r =>
                RfqDtoMapper.ToListItemDto(r, bidCountsByRfq.TryGetValue(r.Id, out var count) ? count : 0))
        ];
    }
}
