using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.RescindInvitation;

public sealed class RescindInvitationCommandHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IReasonCodeValidator reasonCodes)
    : ICommandHandler<RescindInvitationCommand, Guid>
{
    public async ValueTask<Guid> Handle(RescindInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await reasonCodes.EnsureActiveAsync(CustomListKeys.RfqRescind, command.ReasonCode, cancellationToken)
            .ConfigureAwait(false);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        bool vendorHasSubmittedBid = await dbContext.Bids.AnyAsync(
            b => b.RfqId == command.RfqId
                 && b.VendorId == command.VendorId
                 && b.Submitted
                 && b.WithdrawnUtc == null,
            cancellationToken).ConfigureAwait(false);

        rfq.RescindInvitation(
            command.VendorId,
            command.ReasonCode.Trim().ToUpperInvariant(),
            command.Note,
            DateTime.UtcNow,
            vendorHasSubmittedBid,
            currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
