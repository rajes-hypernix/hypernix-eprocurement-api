using System.Collections.ObjectModel;
using FSH.Framework.Mailing;
using FSH.Framework.Mailing.Services;
using FSH.Framework.Mailing.Templates;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Microsoft.Extensions.Options;

namespace FSH.Modules.Suppliers.Services.Onboarding;

public sealed class OnboardingNotifier(
    IMailService mailService,
    IEmailTemplateRenderer emailTemplates,
    IOptions<OnboardingOptions> options) : IOnboardingNotifier
{
    private OnboardingOptions Options => options.Value;

    public string BuildMagicLink(string rawToken) =>
        $"{Options.PortalBaseUrl.TrimEnd('/')}/onboard?token={Uri.EscapeDataString(rawToken)}";

    public Task SendInvitationAsync(VendorOnboardingInvitation invitation, string rawToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        string link = BuildMagicLink(rawToken);
        string body = emailTemplates.Render(
            EmailTemplateNames.OnboardingInvite,
            new Dictionary<string, string?>
            {
                ["UserName"] = DisplayNameFromEmail(invitation.Email),
                ["InvitedByName"] = string.IsNullOrWhiteSpace(invitation.InvitedByName)
                    ? "Your buyer"
                    : invitation.InvitedByName,
                ["ExpiresUtc"] = invitation.ExpiresUtc.ToString("u"),
                ["ActionUrl"] = link,
            });
        return SendAsync(invitation.Email, "Vendor onboarding invitation", body, cancellationToken);
    }

    public Task SendClarificationAsync(VendorOnboardingApplication application, string rawToken, OnboardingClarificationRound round, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(round);
        string link = BuildMagicLink(rawToken);
        string body = emailTemplates.Render(
            EmailTemplateNames.OnboardingClarification,
            new Dictionary<string, string?>
            {
                ["UserName"] = DisplayNameFromEmail(application.Email),
                ["ApplicationCode"] = application.Code,
                ["Message"] = round.Message,
                ["ActionUrl"] = link,
            });
        return SendAsync(application.Email, $"Clarification requested — {application.Code}", body, cancellationToken);
    }

    public Task SendApprovedAsync(VendorOnboardingApplication application, VendorUser vendorUser, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(vendorUser);
        string body = emailTemplates.Render(
            EmailTemplateNames.OnboardingApproved,
            new Dictionary<string, string?>
            {
                ["UserName"] = string.IsNullOrWhiteSpace(vendorUser.Name)
                    ? DisplayNameFromEmail(application.Email)
                    : vendorUser.Name,
                ["ApplicationCode"] = application.Code,
                ["VendorCode"] = vendorUser.Code,
            });
        return SendAsync(application.Email, $"Onboarding approved — {application.Code}", body, cancellationToken);
    }

    public Task SendRejectedAsync(VendorOnboardingApplication application, string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        string body = emailTemplates.Render(
            EmailTemplateNames.OnboardingRejected,
            new Dictionary<string, string?>
            {
                ["UserName"] = DisplayNameFromEmail(application.Email),
                ["ApplicationCode"] = application.Code,
                ["Reason"] = reason,
            });
        return SendAsync(application.Email, $"Onboarding decision — {application.Code}", body, cancellationToken);
    }

    private Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        string recipient = string.IsNullOrWhiteSpace(Options.TestRecipientOverride) ? email : Options.TestRecipientOverride;
        var request = new MailRequest(new Collection<string> { recipient }, subject, body);
        return mailService.SendAsync(request, cancellationToken);
    }

    private static string DisplayNameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "there";
        }

        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? email[..at] : email;
    }
}
