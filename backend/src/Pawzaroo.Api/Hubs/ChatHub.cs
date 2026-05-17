using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Messaging.Services;

namespace Pawzaroo.Api.Hubs;

/// <summary>
/// Real-time chat hub. Connection lifecycle drives presence; explicit hub
/// methods handle conversation join/leave, typing, mark-as-read, and per-message
/// delivery ACKs. Authoritative state is the database; the hub is a thin
/// fan-out layer over the services.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IPresenceService _presence;
    private readonly IMessagingService _messaging;

    public ChatHub(IPresenceService presence, IMessagingService messaging)
    {
        _presence = presence;
        _messaging = messaging;
    }

    private Guid? UserId
    {
        get
        {
            var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var g) ? g : null;
        }
    }

    // ---------- Lifecycle ----------

    public override async Task OnConnectedAsync()
    {
        if (UserId is { } uid)
        {
            await _presence.TrackConnectionAsync(uid, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(uid));
            await Clients.Others.SendAsync("presence", new { userId = uid, online = true });
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserId is { } uid)
        {
            await _presence.UntrackConnectionAsync(uid, Context.ConnectionId);
            var stillOnline = await _presence.IsOnlineAsync(uid);
            if (!stillOnline)
                await Clients.Others.SendAsync("presence", new { userId = uid, online = false });
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ---------- Conversation membership ----------

    public Task JoinConversation(string conversationId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ConvGroup(conversationId));

    public Task LeaveConversation(string conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ConvGroup(conversationId));

    // ---------- Typing ----------

    public Task Typing(string conversationId, bool isTyping) =>
        Clients.OthersInGroup(ConvGroup(conversationId)).SendAsync("typing", new
        {
            conversationId, userId = UserId, isTyping
        });

    // ---------- Read + delivery ACKs ----------

    public async Task MarkRead(string conversationId, string? lastMessageId)
    {
        if (!Guid.TryParse(conversationId, out var cid)) return;
        Guid? lmid = Guid.TryParse(lastMessageId, out var p) ? p : null;
        var last = await _messaging.MarkReadAsync(cid, lmid);
        await Clients.OthersInGroup(ConvGroup(cid)).SendAsync("read", new
        {
            conversationId = cid, userId = UserId, lastMessageId = last, readAt = DateTime.UtcNow
        });
    }

    public async Task AckDelivered(string messageId)
    {
        if (!Guid.TryParse(messageId, out var mid)) return;
        await _messaging.AckDeliveredAsync(mid);
        await Clients.All.SendAsync("delivered", new { messageId = mid, userId = UserId, at = DateTime.UtcNow });
    }

    // ---------- Helpers ----------

    private static string ConvGroup(string conversationId) => $"conv:{conversationId}";
    private static string ConvGroup(Guid conversationId) => $"conv:{conversationId}";
    private static string UserGroup(Guid userId) => $"user:{userId}";
}
