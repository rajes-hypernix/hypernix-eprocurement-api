using eProcure.Application.Abstractions;
using eProcure.Application.Onboarding;
using eProcure.Domain.Onboarding;
using Microsoft.Extensions.Options;

namespace eProcure.Infrastructure.Email;

/// <summary>
/// Composes onboarding emails and sends them through <see cref="IEmailSender"/>. Applies the
/// <b>recipient override</b> here (VENDOR-ONBOARDING-SPEC §6, E3): when
/// <see cref="OnboardingOptions.TestRecipientOverride"/> is set, EVERY onboarding message is routed
/// to that address — so the whole flow is testable without real vendor inboxes. The override key is
/// configured ONLY in non-production appsettings (it is absent from the production config), which is
/// how "non-production" is enforced. The magic link is built from the configured portal base URL.
/// </summary>
public sealed class OnboardingNotifier(
    IEmailSender email, IOptions<OnboardingOptions> options) : IOnboardingNotifier
{
    private readonly OnboardingOptions _opt = options.Value;

    public string BuildMagicLink(string rawToken) =>
        $"{_opt.PortalBaseUrl.TrimEnd('/')}/?t={Uri.EscapeDataString(rawToken)}#onboard";

    public Task SendInvitationAsync(VendorOnboardingInvitation invitation, string applicationCode,
        string rawToken, CancellationToken ct = default)
    {
        var link = BuildMagicLink(rawToken);
        var typeLabel = invitation.Type == Domain.Suppliers.VendorType.Swec ? "PETRONAS SWEC" : "Non-SWEC";
        var body =
            $"""
            <p>Dear Supplier,</p>
            <p>You have been invited to register as a supplier with Sarawak Petchem (SPSB) as a
            <b>{typeLabel}</b> vendor. Please complete your onboarding via the secure link below.</p>
            <p><a href="{link}">Start your onboarding</a></p>
            <p>This link is valid for {_opt.LinkExpiryDays} days and needs no password. You can save and
            return any time via the same link. Reference: <b>{applicationCode}</b>.</p>
            <p>Regards,<br/>SPSB Procurement</p>
            """;

        var message = new EmailMessage(RecipientFor(invitation.Email),
            "Complete your SPSB supplier onboarding", body);
        return email.SendAsync(message, ct);
    }

    public Task SendClarificationAsync(string toEmail, string applicationCode, string message,
        IReadOnlyList<(string Topic, string Request)> items, CancellationToken ct = default)
    {
        var list = string.Concat(items.Select(i => $"<li><b>{Enc(i.Topic)}</b> — {Enc(i.Request)}</li>"));
        var body =
            $"""
            <p>SPSB procurement has reviewed your application <b>{applicationCode}</b> and needs a few items
            clarified before proceeding:</p>
            <p>{Enc(message)}</p>
            <ul>{list}</ul>
            <p>Please address them all and resubmit via your onboarding link.</p>
            """;
        return email.SendAsync(new EmailMessage(RecipientFor(toEmail),
            $"Clarification requested — {applicationCode}", body), ct);
    }

    public Task SendApprovedAsync(string toEmail, string applicationCode, string vendorCode,
        string setPasswordLink, CancellationToken ct = default)
    {
        var body =
            $"""
            <p>Congratulations — your application <b>{applicationCode}</b> has been approved and you are now
            registered as SPSB vendor <b>{vendorCode}</b>.</p>
            <p>Set your supplier-portal password to get started: <a href="{setPasswordLink}">Set password</a></p>
            """;
        return email.SendAsync(new EmailMessage(RecipientFor(toEmail),
            "Your SPSB supplier registration is approved", body), ct);
    }

    public Task SendRejectedAsync(string toEmail, string applicationCode, string reason, CancellationToken ct = default)
    {
        var body =
            $"""
            <p>Thank you for your interest. After review, application <b>{applicationCode}</b> was not approved
            at this time.</p>
            <p><b>Reason:</b> {Enc(reason)}</p>
            """;
        return email.SendAsync(new EmailMessage(RecipientFor(toEmail),
            $"Update on your SPSB supplier application — {applicationCode}", body), ct);
    }

    /// <summary>HTML-encodes buyer/vendor-entered values interpolated into email bodies.</summary>
    private static string Enc(string s) => System.Net.WebUtility.HtmlEncode(s);

    /// <summary>The effective recipient: the configured test override if present, else the real address.</summary>
    private string RecipientFor(string intended) =>
        !string.IsNullOrWhiteSpace(_opt.TestRecipientOverride) ? _opt.TestRecipientOverride! : intended;
}
