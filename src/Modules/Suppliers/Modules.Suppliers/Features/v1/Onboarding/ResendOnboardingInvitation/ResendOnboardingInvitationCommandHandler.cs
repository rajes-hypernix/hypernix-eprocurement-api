using System.Security.Cryptography;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Services.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.ResendOnboardingInvitation;

public sealed class ResendOnboardingInvitationCommandHandler(
    SuppliersDbContext dbContext,
    IOnboardingNotifier notifier,
    IOptions<OnboardingOptions> options)
    : ICommandHandler<ResendOnboardingInvitationCommand, OnboardingInvitationDto>
{
    public async ValueTask<OnboardingInvitationDto> Handle(ResendOnboardingInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await dbContext.VendorOnboardingInvitations
            .FirstOrDefaultAsync(i => i.Id == command.InvitationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding invitation {command.InvitationId} not found.");

        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        invitation.Reissue(rawToken, DateTime.UtcNow, options.Value.LinkExpiryDays);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await notifier.SendInvitationAsync(invitation, rawToken, cancellationToken).ConfigureAwait(false);

        string? applicationCode = invitation.ApplicationId is { } appId
            ? await dbContext.VendorOnboardingApplications.AsNoTracking()
                .Where(a => a.Id == appId).Select(a => a.Code).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return OnboardingDtoMapper.ToDto(invitation, applicationCode, notifier.BuildMagicLink(rawToken));
    }
}
