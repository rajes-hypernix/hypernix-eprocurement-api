using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.RescindInvitation;

public sealed class RescindInvitationCommandHandler(SourcingDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<RescindInvitationCommand, Guid>
{
    public async ValueTask<Guid> Handle(RescindInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        // The Bid module doesn't exist yet in this pass, so no vendor can have a submitted bid — always false until Bid lands.
        const bool vendorHasSubmittedBid = false;

        rfq.RescindInvitation(
            command.VendorId,
            command.ReasonCode,
            command.Note,
            DateTime.UtcNow,
            vendorHasSubmittedBid,
            currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
