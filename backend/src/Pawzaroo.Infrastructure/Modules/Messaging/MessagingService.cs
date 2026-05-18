using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Messaging.Dtos;
using Pawzaroo.Application.Modules.Messaging.Events;
using Pawzaroo.Application.Modules.Messaging.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

public class MessagingService : IMessagingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPresenceService _presence;
    private readonly IKafkaProducer _kafka;
    private readonly INotificationService _notify;

    public MessagingService(ApplicationDbContext db, ICurrentUserService current, IPresenceService presence,
        IKafkaProducer kafka, INotificationService notify)
    {
        _db = db;
        _current = current;
        _presence = presence;
        _kafka = kafka;
        _notify = notify;
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(bool includeArchived, string? search, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();

        var q = _db.ConversationParticipants
            .Where(cp => cp.UserId == uid && !cp.HasLeft);
        if (!includeArchived) q = q.Where(cp => !cp.IsArchived);

        var rows = await q
            .OrderByDescending(cp => cp.Conversation.LastMessageAt ?? cp.Conversation.CreatedAt)
            .Select(cp => new
            {
                cp.ConversationId,
                cp.Conversation.Title,
                cp.Conversation.IsGroup,
                cp.Conversation.ContextType,
                cp.Conversation.ContextRefId,
                cp.Conversation.LastMessageAt,
                cp.UnreadCount,
                cp.IsArchived,
                cp.IsMuted,
                LastMessagePreview = cp.Conversation.Messages
                    .OrderByDescending(m => m.CreatedAt).Select(m => m.Content).FirstOrDefault(),
                Participants = cp.Conversation.Participants
                    .Where(p => !p.HasLeft && p.UserId != uid)
                    .Select(p => new { p.UserId, p.User.DisplayName, p.User.AvatarUrl }).ToList()
            })
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(r =>
                r.Participants.Any(p => p.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
                || (r.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

        var allOtherIds = rows.SelectMany(r => r.Participants.Select(p => p.UserId)).Distinct();
        var online = await _presence.AreOnlineAsync(allOtherIds, ct);

        return rows.Select(r => new ConversationSummaryDto(
            r.ConversationId, r.Title, r.IsGroup, r.ContextType, r.ContextRefId,
            r.LastMessageAt, r.LastMessagePreview, r.UnreadCount, r.IsArchived, r.IsMuted,
            r.Participants.Select(p =>
                new ParticipantDto(p.UserId, p.DisplayName, p.AvatarUrl, online.TryGetValue(p.UserId, out var o) && o)
            ).ToList()
        )).ToList();
    }

    public async Task<ConversationSummaryDto?> GetConversationAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var row = await _db.ConversationParticipants
            .Where(cp => cp.UserId == uid && cp.ConversationId == id)
            .Select(cp => new
            {
                cp.ConversationId, cp.Conversation.Title, cp.Conversation.IsGroup,
                cp.Conversation.ContextType, cp.Conversation.ContextRefId,
                cp.Conversation.LastMessageAt, cp.UnreadCount, cp.IsArchived, cp.IsMuted,
                Participants = cp.Conversation.Participants
                    .Where(p => !p.HasLeft && p.UserId != uid)
                    .Select(p => new { p.UserId, p.User.DisplayName, p.User.AvatarUrl }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (row is null) return null;

        var online = await _presence.AreOnlineAsync(row.Participants.Select(p => p.UserId), ct);
        return new ConversationSummaryDto(
            row.ConversationId, row.Title, row.IsGroup, row.ContextType, row.ContextRefId,
            row.LastMessageAt, null, row.UnreadCount, row.IsArchived, row.IsMuted,
            row.Participants.Select(p => new ParticipantDto(p.UserId, p.DisplayName, p.AvatarUrl,
                online.TryGetValue(p.UserId, out var o) && o)).ToList());
    }

    public async Task<Guid> StartConversationAsync(StartConversationInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (uid == input.OtherUserId) throw new ConflictException("Can't start a conversation with yourself.");

        await EnsureNotBlockedAsync(uid, input.OtherUserId, ct);

        // Reuse 1:1 conversations within the same context (or no context).
        var existing = await _db.Conversations
            .Where(c => !c.IsGroup
                     && c.ContextType == input.ContextType
                     && c.ContextRefId == input.ContextRefId
                     && c.Participants.Any(p => p.UserId == uid)
                     && c.Participants.Any(p => p.UserId == input.OtherUserId))
            .Select(c => c.Id).FirstOrDefaultAsync(ct);

        Guid convId;
        if (existing != Guid.Empty)
        {
            convId = existing;
        }
        else
        {
            var conv = new Conversation
            {
                IsGroup = false,
                ContextType = input.ContextType,
                ContextRefId = input.ContextRefId,
                LastMessageAt = DateTime.UtcNow
            };
            conv.Participants.Add(new ConversationParticipant { UserId = uid });
            conv.Participants.Add(new ConversationParticipant { UserId = input.OtherUserId });
            _db.Conversations.Add(conv);
            await _db.SaveChangesAsync(ct);
            convId = conv.Id;

            await _kafka.PublishAsync(MessagingTopics.Messages,
                new ConversationStarted(convId, uid, input.OtherUserId, input.ContextType, input.ContextRefId, DateTime.UtcNow),
                convId.ToString(), ct);
        }

        if (!string.IsNullOrWhiteSpace(input.InitialMessage))
        {
            await SendMessageAsync(new SendMessageInput(convId, "Text", input.InitialMessage, null, null, null), ct);
        }
        return convId;
    }

    public async Task<CursorPage<MessageDto>> GetMessagesAsync(Guid conversationId, string? cursor, int pageSize, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!await _db.ConversationParticipants.AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == uid, ct))
            throw new ForbiddenException();

        var q = _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && !m.IsDeletedForAll);

        var cur = MessagingCursor.Decode(cursor);
        if (cur is { } c)
            q = q.Where(m => m.CreatedAt < c.CreatedAt || (m.CreatedAt == c.CreatedAt && m.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(pageSize, 1, 100);
        var msgs = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(take + 1)
            .Select(m => new
            {
                m.Id, m.ConversationId, m.SenderId, SenderName = m.Sender.DisplayName,
                SenderAvatarUrl = m.Sender.AvatarUrl,
                Type = m.Type.ToString(), m.Content, m.MediaUrl, m.ReplyToMessageId,
                m.IsEdited, m.IsDeletedForAll, m.CreatedAt,
                Attachments = _db.MessageAttachments.Where(a => a.MessageId == m.Id)
                    .Select(a => new AttachmentDto(a.Url, a.MimeType, a.SizeBytes, a.FileName, a.Width, a.Height))
                    .ToList(),
                MyReceipt = _db.MessageReadReceipts
                    .Where(r => r.MessageId == m.Id && r.UserId == uid)
                    .Select(r => new { r.DeliveredAt, r.ReadAt }).FirstOrDefault()
            })
            .ToListAsync(ct);

        string? next = null;
        if (msgs.Count > take)
        {
            var last = msgs[take - 1];
            next = MessagingCursor.Encode(last.CreatedAt, last.Id);
            msgs.RemoveAt(msgs.Count - 1);
        }

        // Return oldest -> newest for easier UI prepend on load-more.
        msgs.Reverse();
        var items = msgs.Select(m => new MessageDto(
            m.Id, m.ConversationId, m.SenderId, m.SenderName, m.SenderAvatarUrl,
            m.Type, m.Content, m.MediaUrl, m.ReplyToMessageId, m.IsEdited, m.IsDeletedForAll,
            m.CreatedAt, m.Attachments,
            m.MyReceipt?.DeliveredAt, m.MyReceipt?.ReadAt
        )).ToList();
        return new CursorPage<MessageDto>(items, next);
    }

    public async Task<MessageDto> SendMessageAsync(SendMessageInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var conv = await _db.Conversations.Include(c => c.Participants)
            .SingleOrDefaultAsync(c => c.Id == input.ConversationId, ct)
            ?? throw new NotFoundException("Conversation", input.ConversationId);
        if (!conv.Participants.Any(p => p.UserId == uid && !p.HasLeft))
            throw new ForbiddenException();

        // Block check: refuse if any other participant has blocked us (or we them).
        var otherIds = conv.Participants.Where(p => p.UserId != uid).Select(p => p.UserId).ToArray();
        if (await _db.UserBlocks.AnyAsync(b =>
            (b.BlockerId == uid && otherIds.Contains(b.BlockedUserId))
            || (otherIds.Contains(b.BlockerId) && b.BlockedUserId == uid), ct))
            throw new ForbiddenException("You can't message this user.");

        if (!Enum.TryParse<MessageType>(input.Type, true, out var msgType))
            throw new AppException("invalid_type", $"Unknown message type '{input.Type}'.");

        var msg = new Message
        {
            ConversationId = input.ConversationId,
            SenderId = uid,
            Type = msgType,
            Content = input.Content,
            MediaUrl = input.MediaUrl,
            ReplyToMessageId = input.ReplyToMessageId
        };
        _db.Messages.Add(msg);

        if (input.Attachments is { Count: > 0 })
            foreach (var a in input.Attachments)
                _db.MessageAttachments.Add(new MessageAttachment
                {
                    Message = msg, MessageId = msg.Id,
                    Url = a.Url, MimeType = a.MimeType, SizeBytes = a.SizeBytes,
                    FileName = a.FileName, Width = a.Width, Height = a.Height
                });

        conv.LastMessageAt = DateTime.UtcNow;
        foreach (var p in conv.Participants.Where(p => p.UserId != uid))
        {
            p.UnreadCount++;
            if (p.IsArchived) p.IsArchived = false; // un-archive on new activity
        }

        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MessagingTopics.Messages,
            new MessageSent(msg.Id, conv.Id, uid, otherIds, DateTime.UtcNow), msg.Id.ToString(), ct);

        // Push in-app notification to muted-off recipients.
        foreach (var p in conv.Participants.Where(p => p.UserId != uid && !p.IsMuted))
            await _notify.NotifyUserAsync(p.UserId, "New message",
                input.Content is { Length: > 0 } ? Trim(input.Content!, 80) : "Sent you a message",
                new { conversationId = conv.Id, messageId = msg.Id }, ct);

        var sender = await _db.Users.Where(u => u.Id == uid)
            .Select(u => new { u.DisplayName, u.AvatarUrl }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", uid);

        var dto = new MessageDto(msg.Id, conv.Id, uid, sender.DisplayName, sender.AvatarUrl,
            msg.Type.ToString(), msg.Content, msg.MediaUrl, msg.ReplyToMessageId,
            msg.IsEdited, msg.IsDeletedForAll, msg.CreatedAt,
            input.Attachments?.Select(a => new AttachmentDto(a.Url, a.MimeType, a.SizeBytes, a.FileName, a.Width, a.Height)).ToList()
                ?? new List<AttachmentDto>(),
            null, null);

        // Fan out to every participant's user-group on the chat hub so all of
        // their connected clients see the message immediately — even the ones
        // that haven't joined the conversation-specific group (sidebar badge,
        // conversations list, other tabs).
        var allParticipantIds = conv.Participants.Where(p => !p.HasLeft).Select(p => p.UserId).ToList();
        await _notify.PushChatMessageToUsersAsync(allParticipantIds, dto, ct);

        return dto;
    }

    public async Task DeleteMessageAsync(Guid messageId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var m = await _db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct)
            ?? throw new NotFoundException("Message", messageId);
        var byModerator = m.SenderId != uid;
        if (byModerator && !_current.Permissions.Contains(Application.Common.Permissions.Permissions.Messaging.Delete))
            throw new ForbiddenException();
        m.IsDeletedForAll = true;
        m.Content = null;
        m.MediaUrl = null;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MessagingTopics.Messages,
            new MessageDeleted(messageId, uid, byModerator, DateTime.UtcNow), messageId.ToString(), ct);
    }

    public async Task<Guid?> MarkReadAsync(Guid conversationId, Guid? lastMessageId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var participant = await _db.ConversationParticipants
            .SingleOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == uid, ct)
            ?? throw new ForbiddenException();

        DateTime? boundaryCreatedAt = null;
        if (lastMessageId.HasValue)
            boundaryCreatedAt = await _db.Messages.AsNoTracking()
                .Where(m => m.Id == lastMessageId.Value && m.ConversationId == conversationId)
                .Select(m => (DateTime?)m.CreatedAt).SingleOrDefaultAsync(ct);

        var now = DateTime.UtcNow;
        var unread = _db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != uid
                     && !_db.MessageReadReceipts.Any(r => r.MessageId == m.Id && r.UserId == uid && r.ReadAt != null)
                     && (boundaryCreatedAt == null || m.CreatedAt <= boundaryCreatedAt));

        var unreadIds = await unread.Select(m => m.Id).ToListAsync(ct);
        foreach (var mid in unreadIds)
        {
            var existing = await _db.MessageReadReceipts
                .SingleOrDefaultAsync(r => r.MessageId == mid && r.UserId == uid, ct);
            if (existing is null)
                _db.MessageReadReceipts.Add(new MessageReadReceipt { MessageId = mid, UserId = uid, DeliveredAt = now, ReadAt = now });
            else
            {
                existing.DeliveredAt ??= now;
                existing.ReadAt = now;
            }
        }

        participant.LastReadAt = now;
        participant.UnreadCount = 0;
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MessagingTopics.Messages,
            new MessageRead(conversationId, uid, unreadIds.LastOrDefault(), now),
            conversationId.ToString(), ct);
        return unreadIds.LastOrDefault();
    }

    public async Task AckDeliveredAsync(Guid messageId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var now = DateTime.UtcNow;
        var receipt = await _db.MessageReadReceipts
            .SingleOrDefaultAsync(r => r.MessageId == messageId && r.UserId == uid, ct);
        if (receipt is null)
            _db.MessageReadReceipts.Add(new MessageReadReceipt { MessageId = messageId, UserId = uid, DeliveredAt = now });
        else
            receipt.DeliveredAt ??= now;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MessagingTopics.Messages,
            new MessageDelivered(messageId, uid, now), messageId.ToString(), ct);
    }

    public async Task SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var p = await _db.ConversationParticipants
            .SingleOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == uid, ct)
            ?? throw new ForbiddenException();
        p.IsArchived = archived;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetMutedAsync(Guid conversationId, bool muted, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var p = await _db.ConversationParticipants
            .SingleOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == uid, ct)
            ?? throw new ForbiddenException();
        p.IsMuted = muted;
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> GetTotalUnreadAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return _db.ConversationParticipants
            .Where(cp => cp.UserId == uid && !cp.HasLeft && !cp.IsArchived)
            .SumAsync(cp => cp.UnreadCount, ct);
    }

    private async Task EnsureNotBlockedAsync(Guid me, Guid other, CancellationToken ct)
    {
        if (await _db.UserBlocks.AnyAsync(b =>
            (b.BlockerId == me && b.BlockedUserId == other)
            || (b.BlockerId == other && b.BlockedUserId == me), ct))
            throw new ForbiddenException("Blocked.");
    }

    private static string Trim(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}
