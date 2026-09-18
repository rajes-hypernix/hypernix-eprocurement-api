using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Evaluation;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Features.v1.Rfqs;
using FSH.Modules.Sourcing.Services;
using Mediator;

namespace FSH.Modules.Sourcing.Features.v1.Evaluation.ListBidOpenings;

public sealed class ListBidOpeningsQueryHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService)
    : IQueryHandler<ListBidOpeningsQuery, IReadOnlyList<RfqListItemDto>>
{
    public async ValueTask<IReadOnlyList<RfqListItemDto>> Handle(
        ListBidOpeningsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (currentUser.GetVendorId() is not null)
        {
            throw new ForbiddenException("Vendors cannot list sealed bid openings.");
        }

        if (!await BidOpeningAccess.CanViewAsync(userService, currentUser.GetUserId().ToString(), cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ForbiddenException("Not authorized to view bid openings.");
        }

        return await RfqListLoader.LoadAsync(dbContext, currentUser, openingsOnly: true, cancellationToken)
            .ConfigureAwait(false);
    }
}
