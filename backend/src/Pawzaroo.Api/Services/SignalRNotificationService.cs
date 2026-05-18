using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Pawzaroo.Api.Hubs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _notifications;
    private readonly IHubContext<ChatHub> _chat;
    private readonly ApplicationDbContext _db;

    public SignalRNotificationService(IHubContext<NotificationHub> notifications, IHubContext<ChatHub> chat, ApplicationDbContext db)
    {
        _notifications = notifications;
        _chat = chat;
        _db = db;
    }

    public async Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default)
    {
        var payloadJson = payload is not null ? JsonSerializer.Serialize(payload) : null;

        var notification = new InAppNotification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Payload = payloadJson,
            IsRead = false,
            CreatedBy = userId
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        await _notifications.Clients.Group($"user:{userId}")
            .SendAsync("notify", new { id = notification.Id, title, body, payload, at = notification.CreatedAt }, ct);
    }

    public async Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default)
    {
        await _notifications.Clients.All.SendAsync("notify", new { title, body, payload, at = DateTime.UtcNow }, ct);
    }

    public Task PushMessageToConversationAsync(Guid conversationId, object message, CancellationToken ct = default)
        => _chat.Clients.Group($"conv:{conversationId}").SendAsync("message", message, ct);

    public async Task PushChatMessageToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken ct = default)
    {
        foreach (var uid in userIds.Distinct())
            await _chat.Clients.Group($"user:{uid}").SendAsync("message", payload, ct);
    }
}
