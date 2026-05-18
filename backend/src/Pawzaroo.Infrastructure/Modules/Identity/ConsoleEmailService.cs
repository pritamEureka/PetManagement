using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Modules.Identity;

/// <summary>
/// Stub email transport. Logs the message body and, when a user id is supplied,
/// also fires an in-app SignalR notification so the recipient sees the message
/// the next time they sign in. Swap for MailKit/SendGrid/SES in prod by
/// replacing only this class in the DI registration.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly INotificationService _notify;
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(INotificationService notify, ILogger<ConsoleEmailService> logger)
    {
        _notify = notify;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, Guid? userId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[email] to={To} subject={Subject}\n{Body}", toEmail, subject, body);

        if (userId is { } uid)
        {
            // Best-effort: an in-app notification is a poor man's email for the demo.
            // Failures here must not break the calling workflow.
            try { await _notify.NotifyUserAsync(uid, subject, body, null, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver in-app notification mirror for email to {To}", toEmail);
            }
        }
    }
}
