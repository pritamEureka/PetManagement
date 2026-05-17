using Microsoft.AspNetCore.SignalR;
using Pawzaroo.Api.Hubs;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Api.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _notifications;
    private readonly IHubContext<ChatHub> _chat;

    public SignalRNotificationService(IHubContext<NotificationHub> notifications, IHubContext<ChatHub> chat)
    {
        _notifications = notifications;
        _chat = chat;
    }

    public Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default)
        => _notifications.Clients.Group($"user:{userId}").SendAsync("notify", new { title, body, payload, at = DateTime.UtcNow }, ct);

    public Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default)
        => _notifications.Clients.All.SendAsync("notify", new { title, body, payload, at = DateTime.UtcNow }, ct);

    public Task PushMessageToConversationAsync(Guid conversationId, object message, CancellationToken ct = default)
        => _chat.Clients.Group($"conv:{conversationId}").SendAsync("message", message, ct);
}
