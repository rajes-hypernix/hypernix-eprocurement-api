using FSH.Framework.Eventing.Abstractions;
using FSH.Framework.Mailing;
using FSH.Framework.Mailing.Services;
using FSH.Framework.Mailing.Templates;
using FSH.Modules.Identity.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace FSH.Modules.Identity.Events;

/// <summary>
/// Sends a welcome email when a new user registers.
/// </summary>
public sealed class UserRegisteredEmailHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IMailService _mailService;
    private readonly IEmailTemplateRenderer _emailTemplates;
    private readonly ILogger<UserRegisteredEmailHandler> _logger;

    public UserRegisteredEmailHandler(
        IMailService mailService,
        IEmailTemplateRenderer emailTemplates,
        ILogger<UserRegisteredEmailHandler> logger)
    {
        _mailService = mailService;
        _emailTemplates = emailTemplates;
        _logger = logger;
    }

    public async Task HandleAsync(UserRegisteredIntegrationEvent @event, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (string.IsNullOrWhiteSpace(@event.Email))
        {
            return;
        }

        try
        {
            string displayName = string.IsNullOrWhiteSpace(@event.FirstName) ? "there" : @event.FirstName;
            string body = _emailTemplates.Render(
                EmailTemplateNames.Welcome,
                new Dictionary<string, string?>
                {
                    ["UserName"] = displayName,
                });

            var mail = new MailRequest(
                to: new System.Collections.ObjectModel.Collection<string> { @event.Email },
                subject: "Welcome to Hypernix eProcure",
                body: body);

            await _mailService.SendAsync(mail, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Email failures must not break user registration.
            // The email can be retried via the outbox/dead-letter mechanism.
            // PII minimization: identify the recipient by UserId, not email address.
            _logger.LogWarning(ex, "Failed to send welcome email to user {UserId}", @event.UserId);
        }
    }
}
