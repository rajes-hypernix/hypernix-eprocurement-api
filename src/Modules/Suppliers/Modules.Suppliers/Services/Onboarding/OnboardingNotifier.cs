using System.Collections.ObjectModel;
using FSH.Framework.Mailing;
using FSH.Framework.Mailing.Services;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Services.Onboarding;

public sealed class OnboardingNotifier(IMailService mailService, IOptions<OnboardingOptions> options) : IOnboardingNotifier
{
    private OnboardingOptions Options => options.Value;

    public string BuildMagicLink(string rawToken) =>
        $"{Options.PortalBaseUrl.TrimEnd('/')}/onboarding?token={Uri.EscapeDataString(rawToken)}";

    public Task SendInvitationAsync(VendorOnboardingInvitation invitation, string rawToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        string link = BuildMagicLink(rawToken);
        string body = $"You've been invited to complete vendor onboarding with SPSB. " +
            $"Follow this link to get started (valid until {invitation.ExpiresUtc:u}): {link}";
        return SendAsync(invitation.Email, "Vendor onboarding invitation", body, cancellationToken);
    }

    public Task SendClarificationAsync(VendorOnboardingApplication application, string rawToken, OnboardingClarificationRound round, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(round);
        string link = BuildMagicLink(rawToken);
        string body = $"Additional information is required for your onboarding application {application.Code}: " +
            $"{round.Message} Please respond via: {link}";
        return SendAsync(application.Email, $"Clarification requested — {application.Code}", body, cancellationToken);
    }

    public Task SendApprovedAsync(VendorOnboardingApplication application, VendorUser vendorUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(vendorUser);
        string setPasswordLink = $"{Options.PortalBaseUrl.TrimEnd('/')}/?vu={vendorUser.Id}#set-password";
        string body = $"Your onboarding application {application.Code} has been approved. " +
            $"Vendor code: {vendorUser.Code}. Set your portal password here: {setPasswordLink}";
        return SendAsync(application.Email, $"Onboarding approved — {application.Code}", body, cancellationToken);
    }

    public Task SendRejectedAsync(VendorOnboardingApplication application, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        string body = $"Your onboarding application {application.Code} was not approved. Reason: {reason}";
        return SendAsync(application.Email, $"Onboarding decision — {application.Code}", body, cancellationToken);
    }

    private Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        string recipient = string.IsNullOrWhiteSpace(Options.TestRecipientOverride) ? email : Options.TestRecipientOverride;
        var request = new MailRequest(new Collection<string> { recipient }, subject, body);
        return mailService.SendAsync(request, cancellationToken);
    }
}
