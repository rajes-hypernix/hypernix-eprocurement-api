using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;

namespace FSH.Modules.Suppliers.Services.Onboarding;

public interface IOnboardingNotifier
{
    string BuildMagicLink(string rawToken);

    Task SendInvitationAsync(VendorOnboardingInvitation invitation, string rawToken, CancellationToken cancellationToken);

    Task SendClarificationAsync(VendorOnboardingApplication application, string rawToken, OnboardingClarificationRound round, CancellationToken cancellationToken);

    Task SendApprovedAsync(VendorOnboardingApplication application, VendorUser vendorUser, CancellationToken cancellationToken);

    Task SendRejectedAsync(VendorOnboardingApplication application, string reason, CancellationToken cancellationToken);
}
