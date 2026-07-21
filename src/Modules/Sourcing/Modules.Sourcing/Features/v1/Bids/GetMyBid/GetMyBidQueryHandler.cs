using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids.GetMyBid;

public sealed class GetMyBidQueryHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetMyBidQuery, BidDto?>
{
    public async ValueTask<BidDto?> Handle(GetMyBidQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var vendorId = currentUser.RequireVendorId();

        await BidAuthorization.RequireInvitedRfqAsync(dbContext, query.RfqId, vendorId, cancellationToken).ConfigureAwait(false);

        var bid = await dbContext.Bids
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.RfqId == query.RfqId && b.VendorId == vendorId, cancellationToken)
            .ConfigureAwait(false);

        return bid is null ? null : BidDtoMapper.ToDto(bid);
    }
}
