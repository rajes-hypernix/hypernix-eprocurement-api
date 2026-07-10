using System.Net;
using System.Net.Mail;
using eProcure.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eProcure.Infrastructure.Email;

/// <summary>
/// Config-driven SMTP transport for <see cref="IEmailSender"/>. Reads host/credentials from
/// <see cref="EmailOptions"/> (configuration only — no secrets in source, E5). When no SMTP host is
/// configured — the normal case in local dev — it logs the message instead of dialing out, so the
/// onboarding flow is fully exercisable without a mail server.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _opt = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.Smtp.Host))
        {
            logger.LogInformation("[email:log-only] To={To} Subject=\"{Subject}\"\n{Body}",
                message.To, message.Subject, message.HtmlBody);
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(_opt.From, _opt.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);

        using var client = new SmtpClient(_opt.Smtp.Host, _opt.Smtp.Port) { EnableSsl = _opt.Smtp.UseSsl };
        if (!string.IsNullOrEmpty(_opt.Smtp.Username))
            client.Credentials = new NetworkCredential(_opt.Smtp.Username, _opt.Smtp.Password);

        await client.SendMailAsync(mail, ct);
        logger.LogInformation("Sent onboarding email to {To}: {Subject}", message.To, message.Subject);
    }
}
