using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.SendClarification;

public sealed class SendClarificationCommandHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService)
    : ICommandHandler<SendClarificationCommand, IReadOnlyList<ClarificationMessageDto>>
{
    public async ValueTask<IReadOnlyList<ClarificationMessageDto>> Handle(SendClarificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = DateTime.UtcNow;
        var vendorId = currentUser.GetVendorId();
        List<Clarification> created;

        if (vendorId is { } ownVendorId)
        {
            created = [Clarification.Create(command.Scope, ownVendorId, ClarificationSenderKind.Vendor, "Vendor", null, command.Body, published: false, now)];
        }
        else
        {
            bool allowed = await userService
                .HasPermissionAsync(currentUser.GetUserId().ToString(), SourcingPermissions.Clarifications.Send, cancellationToken)
                .ConfigureAwait(false);
            if (!allowed)
            {
                throw new ForbiddenException("Clarifications send permission is required.");
            }

            string senderName = currentUser.Name ?? "Buyer";

            if (command.Published && !string.Equals(command.Scope, "general", StringComparison.OrdinalIgnoreCase))
            {
                var rfq = await dbContext.Rfqs
                    .Include(r => r.Invitations)
                    .FirstOrDefaultAsync(r => r.Code == command.Scope, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new NotFoundException($"RFQ {command.Scope} not found.");

                created = [.. rfq.LiveInvitedVendorIds.Select(v =>
                    Clarification.Create(command.Scope, v, ClarificationSenderKind.Buyer, senderName, null, command.Body, published: true, now))];
            }
            else
            {
                if (command.VendorId is not { } targetVendorId)
                {
                    throw new SourcingRuleException("A targeted clarification reply requires a VendorId.");
                }

                created = [Clarification.Create(command.Scope, targetVendorId, ClarificationSenderKind.Buyer, senderName, null, command.Body, published: false, now)];
            }
        }

        dbContext.Clarifications.AddRange(created);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return [.. created.Select(ClarificationDtoMapper.ToDto)];
    }
}
