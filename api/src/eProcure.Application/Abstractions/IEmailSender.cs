namespace eProcure.Application.Abstractions;

/// <summary>An outbound email. HTML is the primary body; <see cref="TextBody"/> is an optional fallback.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

/// <summary>
/// Transport abstraction for outbound email (VENDOR-ONBOARDING-SPEC §6). The concrete transport is
/// config-driven (SMTP settings come from configuration — NO secrets in code); tests substitute a
/// fake to assert recipient / subject / link. The non-production recipient override is applied one
/// layer up, in the onboarding notifier, so it covers every onboarding message.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
