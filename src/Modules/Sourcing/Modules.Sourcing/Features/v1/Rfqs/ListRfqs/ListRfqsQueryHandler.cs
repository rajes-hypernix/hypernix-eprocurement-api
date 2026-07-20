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
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rfqs.Select(RfqDtoMapper.ToListItemDto)];
    }
}
