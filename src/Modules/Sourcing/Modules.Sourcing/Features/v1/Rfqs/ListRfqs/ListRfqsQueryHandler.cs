using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Features.v1.Rfqs;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.ListRfqs;

public sealed class ListRfqsQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListRfqsQuery, IReadOnlyList<RfqListItemDto>>
{
    public async ValueTask<IReadOnlyList<RfqListItemDto>> Handle(ListRfqsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await RfqListLoader.LoadAsync(dbContext, currentUser, openingsOnly: false, cancellationToken)
            .ConfigureAwait(false);
    }
}
