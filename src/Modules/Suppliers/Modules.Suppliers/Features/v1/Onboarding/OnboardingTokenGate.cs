using FSH.Framework.Core.Exceptions;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding;

/// <summary>
/// Shared gate for every anonymous vendor-facing onboarding endpoint: the magic-link token IS the
/// access scope (no login). Resolves the invitation by token hash and rejects revoked/expired links
/// before returning the linked application.
/// </summary>
internal static class OnboardingTokenGate
{
    internal static async Task<(VendorOnboardingInvitation Invitation, VendorOnboardingApplication Application)> ResolveAsync(
        SuppliersDbContext dbContext, string rawToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        string hash = VendorOnboardingInvitation.HashToken(rawToken);
        var invitation = await dbContext.VendorOnboardingInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("The onboarding link is not valid.");

        if (invitation.Status == OnboardingInvitationStatus.Revoked)
        {
            throw new OnboardingRuleException("This onboarding invitation has been revoked.");
        }

        if (invitation.Status == OnboardingInvitationStatus.Expired || invitation.IsExpired(DateTime.UtcNow))
        {
            throw new OnboardingRuleException("This onboarding link has expired.");
        }

        var application = invitation.ApplicationId is { } applicationId
            ? await dbContext.VendorOnboardingApplications
                .Include(a => a.Rounds)
                .Include(a => a.Financial)
                .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                .ConfigureAwait(false)
            : null;

        if (application is null)
        {
            throw new NotFoundException("The onboarding application for this link could not be found.");
        }

        return (invitation, application);
    }
}
