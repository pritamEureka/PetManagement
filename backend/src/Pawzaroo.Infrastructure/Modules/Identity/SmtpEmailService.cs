using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Modules.Identity;

/// <summary>
/// Bound from the <c>Smtp</c> configuration section. Leave <see cref="Host"/>
/// or <see cref="User"/> empty to fall back to <see cref="ConsoleEmailService"/>
/// (handy for local dev where you don't have credentials yet).
/// </summary>
public class SmtpOptions
{
    public string Host { get; set; } = "";          // e.g. smtp.gmail.com
    public int Port { get; set; } = 587;            // STARTTLS port
    public string User { get; set; } = "";          // SMTP login (usually the From address)
    public string Password { get; set; } = "";      // For Gmail this is an App Password, NOT the account password
    public string FromAddress { get; set; } = "";   // If blank, defaults to User
    public string FromName { get; set; } = "Pawzaroo";
    public bool UseStartTls { get; set; } = true;   // false = implicit SSL (port 465)
}

/// <summary>
/// MailKit-based SMTP transport for <see cref="IEmailService"/>. Mirrors the
/// message to an in-app notification (same as the console stub) so the user
/// gets the alert in the SignalR bell even before the SMTP send returns.
/// SMTP failures are logged but do NOT throw — a missed email shouldn't cancel
/// the calling workflow (approval, suspension, etc.).
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpOptions _opt;
    private readonly INotificationService _notify;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpOptions> opt, INotificationService notify, ILogger<SmtpEmailService> logger)
    {
        _opt = opt.Value;
        _notify = notify;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, Guid? userId = null, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        var fromAddress = string.IsNullOrWhiteSpace(_opt.FromAddress) ? _opt.User : _opt.FromAddress;
        msg.From.Add(new MailboxAddress(_opt.FromName, fromAddress));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { TextBody = body }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secure = _opt.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(_opt.Host, _opt.Port, secure, ct);
            // SMTP AUTH only if credentials were supplied; otherwise assume anonymous relay.
            if (!string.IsNullOrEmpty(_opt.User)) await client.AuthenticateAsync(_opt.User, _opt.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("[email] sent to={To} subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Don't surface SMTP errors to the user — the approve/suspend action
            // itself succeeded. Log loudly so the admin can diagnose.
            _logger.LogError(ex, "[email] SMTP send failed to={To} subject={Subject}", toEmail, subject);
        }

        // Best-effort in-app mirror so the recipient also sees the alert in
        // the bell list the next time they sign in.
        if (userId is { } uid)
        {
            try { await _notify.NotifyUserAsync(uid, subject, body, null, ct); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver in-app notification mirror for email to {To}", toEmail);
            }
        }
    }
}
