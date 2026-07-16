using System.Security.Cryptography;
using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RequestOnboardingClarification;

public sealed class RequestOnboardingClarificationCommandHandler(
    SuppliersDbContext dbContext,
    ICurrentUser currentUser,
    IOnboardingNotifier notifier,
    IOptions<OnboardingOptions> options)
    : ICommandHandler<RequestOnboardingClarificationCommand, OnboardingApplicationDto>
{
    public async ValueTask<OnboardingApplicationDto> Handle(RequestOnboardingClarificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var application = await dbContext.VendorOnboardingApplications
            .Include(a => a.Rounds)
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding application {command.ApplicationId} not found.");

        var now = DateTime.UtcNow;
        var items = command.Items.Select(i => new OnboardingClarificationItem(i.Topic, i.Request));
        application.RequestClarification(
            ClarificationDirection.BuyerToVendor, command.Message,
            currentUser.GetUserId().ToString(), currentUser.Name ?? "Buyer", items, now);

        var round = application.Rounds[^1];

        // Reissue the invitation's magic link so the vendor has a live token to respond with.
        var invitation = application.InvitationId is { } invitationId
            ? await dbContext.VendorOnboardingInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken).ConfigureAwait(false)
            : null;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (invitation is not null)
        {
            string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            invitation.Reissue(rawToken, now, options.Value.LinkExpiryDays);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await notifier.SendClarificationAsync(application, rawToken, round, cancellationToken).ConfigureAwait(false);
        }

        return OnboardingDtoMapper.ToDto(application);
    }
}
