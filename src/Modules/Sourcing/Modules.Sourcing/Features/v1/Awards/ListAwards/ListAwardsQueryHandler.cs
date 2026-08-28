using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Awards.ListAwards;

public sealed class ListAwardsQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListAwardsQuery, IReadOnlyList<AwardListItemDto>>
{
    public async ValueTask<IReadOnlyList<AwardListItemDto>> Handle(ListAwardsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.GetVendorId() is not null)
        {
            return [];
        }

        var awards = await dbContext.Awards.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        if (awards.Count == 0)
        {
            return [];
        }

        var rfqIds = awards.Select(a => a.RfqId).Distinct().ToList();
        var rfqCodes = await dbContext.Rfqs
            .AsNoTracking()
            .Where(r => rfqIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken)
            .ConfigureAwait(false);

        return [.. awards.Select(a => new AwardListItemDto(
            a.Id, a.Code, a.RfqId, rfqCodes.GetValueOrDefault(a.RfqId, string.Empty), a.Status.ToString(), a.TotalValue, a.CreatedUtc, a.ApproverUserId))];
    }
}
