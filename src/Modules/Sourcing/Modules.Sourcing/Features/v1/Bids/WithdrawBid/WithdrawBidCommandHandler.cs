using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids.WithdrawBid;

public sealed class WithdrawBidCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<WithdrawBidCommand, BidDto>
{
    public async ValueTask<BidDto> Handle(WithdrawBidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var vendorId = currentUser.RequireVendorId();
        var now = DateTime.UtcNow;

        var rfq = await BidAuthorization.RequireInvitedRfqAsync(dbContext, command.RfqId, vendorId, cancellationToken).ConfigureAwait(false);

        var bid = await dbContext.Bids
            .FirstOrDefaultAsync(b => b.RfqId == command.RfqId && b.VendorId == vendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("No bid exists to withdraw.");

        bid.Withdraw(now);
        rfq.WithdrawInvitationBid(vendorId, now, actorVendorUserId: currentUser.GetUserId().ToString());

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BidDtoMapper.ToDto(bid);
    }
}
