using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Services;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs.InviteVendor;

public sealed class InviteVendorCommandHandler(
    SourcingDbContext dbContext,
    IMediator mediator,
    ICurrentUser currentUser,
    IOptions<RfqGovernanceOptions> governance)
    : ICommandHandler<InviteVendorCommand, RfqInvitationDto>
{
    public async ValueTask<RfqInvitationDto> Handle(InviteVendorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var rfq = await dbContext.Rfqs
            .Include(r => r.Invitations)
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.Id == command.RfqId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"RFQ {command.RfqId} not found.");

        // Confirms the vendor exists before creating the invitation — Vendor lives in a different schema/module.
        var vendor = await mediator.Send(new GetVendorByIdQuery(command.VendorId), cancellationToken).ConfigureAwait(false);

        var (invitation, _) = rfq.InviteVendor(
            command.VendorId,
            DateTime.UtcNow,
            governance.Value.MinRemainingHoursForLateInvite,
            currentUser.IsAuthenticated() ? currentUser.GetUserId().ToString() : null);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RfqInvitationDto(
            invitation.Id, invitation.VendorId, vendor.Name, vendor.Code, invitation.RoundNumber, invitation.Status.ToString(),
            invitation.DeclineReasonCode, invitation.DeclineNote, invitation.RescindReasonCode, invitation.RescindNote,
            invitation.InvitedUtc, invitation.ViewedUtc, invitation.RespondedUtc, invitation.RescindedUtc);
    }
}
