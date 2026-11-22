namespace Pawzaroo.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public interface IKafkaProducer
{
    Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default);
    Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default);

    /// <summary>
    /// Pushes a chat "message" event to every connection in each user's chat-hub
    /// user group. Lets clients react to incoming messages even when they haven't
    /// joined the conversation-specific group (e.g. for sidebar unread badges or
    /// auto-refreshing the conversations list).
    /// </summary>
    Task PushChatMessageToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken ct = default);
}

/// <summary>
/// Generic transactional email. Stubbed by default (logs + best-effort in-app
/// notification). Swap the implementation for MailKit/SendGrid/SES in prod.
/// </summary>
public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string body, Guid? userId = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a message with file attachments. Transports without attachment support (the console stub) fall back to
    /// inlining text attachments after the body, capped at 200 KB each; binary attachments are listed by name only.
    /// </summary>
    Task SendWithAttachmentsAsync(string toEmail, string subject, string body, IReadOnlyList<EmailAttachment> attachments,
        Guid? userId = null, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder(body);
        foreach (var a in attachments)
        {
            sb.Append("\n\n--- ").Append(a.FileName).Append(" ---\n");
            if (a.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                var text = System.Text.Encoding.UTF8.GetString(a.Content);
                sb.Append(text.Length > 200_000 ? text[..200_000] + "\n... (truncated)" : text);
            }
            else sb.Append("(binary attachment not delivered by this email transport)");
        }
        return SendAsync(toEmail, subject, sb.ToString(), userId, ct);
    }
}

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(Guid userId, CancellationToken ct = default);
}
