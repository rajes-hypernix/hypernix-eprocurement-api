using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.RevokeOnboardingInvitation;

public sealed class RevokeOnboardingInvitationCommandHandler(SuppliersDbContext dbContext)
    : ICommandHandler<RevokeOnboardingInvitationCommand, Guid>
{
    private static readonly OnboardingStatus[] Terminal =
    [
        OnboardingStatus.Approved, OnboardingStatus.Rejected, OnboardingStatus.Expired,
        OnboardingStatus.Revoked, OnboardingStatus.Withdrawn,
    ];

    public async ValueTask<Guid> Handle(RevokeOnboardingInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var invitation = await dbContext.VendorOnboardingInvitations
            .FirstOrDefaultAsync(i => i.Id == command.InvitationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Onboarding invitation {command.InvitationId} not found.");

        var now = DateTime.UtcNow;
        invitation.Revoke(now);

        if (invitation.ApplicationId is { } applicationId)
        {
            var application = await dbContext.VendorOnboardingApplications
                .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                .ConfigureAwait(false);

            if (application is not null && !Terminal.Contains(application.Status))
            {
                application.Revoke(now);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return invitation.Id;
    }
}
