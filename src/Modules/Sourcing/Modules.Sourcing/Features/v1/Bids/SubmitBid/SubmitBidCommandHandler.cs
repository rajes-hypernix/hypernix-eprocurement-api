using FSH.Framework.Core.Context;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Bids;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Bids.SubmitBid;

public sealed class SubmitBidCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<SubmitBidCommand, BidDto>
{
    public async ValueTask<BidDto> Handle(SubmitBidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var vendorId = currentUser.RequireVendorId();
        var now = DateTime.UtcNow;

        var rfq = await BidAuthorization.RequireInvitedRfqAsync(dbContext, command.RfqId, vendorId, cancellationToken).ConfigureAwait(false);
        BidGuards.EnsureOpen(rfq, now);

        var bid = await dbContext.Bids
            .FirstOrDefaultAsync(b => b.RfqId == command.RfqId && b.VendorId == vendorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SourcingRuleException("Save a draft bid before submitting.");

        bid.Submit(now);
        rfq.RecordBidSubmitted(vendorId, now, actorVendorUserId: currentUser.GetUserId().ToString());

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BidDtoMapper.ToDto(bid);
    }
}
