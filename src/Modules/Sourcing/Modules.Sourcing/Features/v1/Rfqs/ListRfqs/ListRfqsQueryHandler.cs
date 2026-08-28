using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ListRfqs;

public sealed class ListRfqsQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListRfqsQuery, IReadOnlyList<RfqListItemDto>>
{
    public async ValueTask<IReadOnlyList<RfqListItemDto>> Handle(ListRfqsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rfqQuery = dbContext.Rfqs
            .AsNoTracking()
            .Include(r => r.Invitations)
            .Include(r => r.Lines)
            .AsQueryable();

        if (currentUser.GetVendorId() is { } vendorId)
        {
            rfqQuery = rfqQuery.Where(r => r.Invitations.Any(i => i.VendorId == vendorId));
        }

        var rfqs = await rfqQuery
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
