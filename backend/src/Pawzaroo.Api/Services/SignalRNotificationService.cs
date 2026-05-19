using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Pawzaroo.Api.Hubs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;

namespace Pawzaroo.Api.Services;

/// <summary>
/// Notification dispatcher — async durable persistence via Kafka, immediate
/// real-time push via the SignalR hub (Redis-backed backplane so cross-instance
/// fan-out works).
///
/// Hot-path flow:
///   1. allocate a NotificationId,
///   2. bump the per-user Redis unread counter (sub-ms, used by the bell icon),
///   3. push to SignalR over the user's group (fans out across pods via the
///      Redis backplane configured in Program.cs),
///   4. publish <see cref="NotificationCreated"/> to Kafka so
///      NotificationDispatcherJob (worker) writes the durable row.
///
/// The synchronous DB insert previously paid for by every like/comment is gone.
/// The Worker is now the *only* writer to the notifications table, which keeps
/// idempotency in one place. If the user is offline when the Kafka event lands,
/// their counter (set in step 2) lags the persisted row by a few ms — acceptable
/// because the very next page load re-reads from cache or DB.
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _notifications;
    private readonly IHubContext<ChatHub> _chat;
    private readonly IKafkaProducer _kafka;
    private readonly INotificationCountCache _counter;
    private readonly INotificationProducerRateLimiter _rateLimit;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> notifications,
        IHubContext<ChatHub> chat,
        IKafkaProducer kafka,
        INotificationCountCache counter,
        INotificationProducerRateLimiter rateLimit,
        ILogger<SignalRNotificationService> logger)
    {
        _notifications = notifications;
        _chat = chat;
        _kafka = kafka;
        _counter = counter;
        _rateLimit = rateLimit;
        _logger = logger;
    }

    public async Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default)
    {
        // Producer-side throttle: skip notifications that exceed N per minute for
        // the same (recipient, title) so a runaway producer (or like-spam bot)
        // can't flood a single user. Aggregation (P1, not yet implemented) is the
        // proper fix — this is a coarse guard for now.
        if (await _rateLimit.ShouldThrottleAsync(userId, title, ct))
        {
            _logger.LogInformation("[notify] throttled for {UserId} title='{Title}'", userId, title);
            return;
        }

        var notificationId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        // 1) Bump Redis counter immediately so the bell icon reflects the new
        //    notification without waiting on the worker to persist.
        await _counter.BumpUnreadAsync(userId, 1, ct);

        // 2) Push to the user's SignalR group. With the Redis backplane this
        //    fans out to every API pod hosting that user's connection.
        await _notifications.Clients.Group($"user:{userId}")
            .SendAsync("notify", new { id = notificationId, title, body, payload, at = occurredAt }, ct);

        // 3) Hand off to the worker for durable persistence. NotificationId is
        //    carried in the event so the worker's insert is idempotent across
        //    Kafka redelivery.
        await _kafka.PublishAsync(
            KafkaTopics.Notifications,
            new NotificationCreated(
                EventId: Guid.NewGuid(),
                NotificationId: notificationId,
                UserId: userId,
                Title: title,
                Body: body,
                Payload: payload,
                OccurredAt: occurredAt),
            key: userId.ToString(),
            ct: ct);
    }

    public Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default)
    {
        // Broadcasts are ephemeral by design — they're shown in the toast/banner,
        // not persisted per-user. If durable system announcements are needed, fan
        // them out as NotificationCreated events per recipient instead.
        return _notifications.Clients.All.SendAsync("notify",
            new { title, body, payload, at = DateTime.UtcNow }, ct);
    }

    public Task PushMessageToConversationAsync(Guid conversationId, object message, CancellationToken ct = default)
        => _chat.Clients.Group($"conv:{conversationId}").SendAsync("message", message, ct);

    public async Task PushChatMessageToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken ct = default)
    {
        foreach (var uid in userIds.Distinct())
            await _chat.Clients.Group($"user:{uid}").SendAsync("message", payload, ct);
    }
}
