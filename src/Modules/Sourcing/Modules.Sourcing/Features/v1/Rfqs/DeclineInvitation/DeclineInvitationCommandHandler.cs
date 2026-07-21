using FSH.Framework.Core.Context;
using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Features.v1.Bids;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.DeclineInvitation;

public sealed class DeclineInvitationCommandHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IReasonCodeValidator reasonCodes)
    : ICommandHandler<DeclineInvitationCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeclineInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var vendorId = currentUser.RequireVendorId();

        await reasonCodes.EnsureActiveAsync(CustomListKeys.BidDecline, command.ReasonCode, cancellationToken)
            .ConfigureAwait(false);

        var rfq = await BidAuthorization.RequireInvitedRfqAsync(dbContext, command.RfqId, vendorId, cancellationToken)
            .ConfigureAwait(false);

        // Events needed for AppendEvent
        await dbContext.Entry(rfq).Collection(r => r.Events).LoadAsync(cancellationToken).ConfigureAwait(false);

        rfq.DeclineInvitation(
            vendorId,
            command.ReasonCode.Trim().ToUpperInvariant(),
            command.Note,
            DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return rfq.Id;
    }
}
