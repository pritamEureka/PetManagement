using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

public record MessageSearchHitDto(Guid MessageId, Guid ConversationId, Guid SenderId, string SenderName, string? Content, DateTime CreatedAt);
public record MessageSearchPage(IReadOnlyList<MessageSearchHitDto> Items, string? NextCursor);
public record PinnedMessageDto(Guid MessageId, Guid ConversationId, string? Content, Guid SenderId, string SenderName, Guid PinnedBy, DateTime PinnedAt);
public record FolderDto(Guid Id, string Name, IReadOnlyList<Guid> ConversationIds);
public record ConversationSettingsDto(Guid ConversationId, int? DisappearAfterSeconds, DateTime? MutedUntil, bool E2eeEnabled);
public record E2eeKeyDto(Guid Id, Guid UserId, string PublicKeyJwk, DateTime CreatedAt);

public interface IMessagingPlusService
{
    Task<MessageSearchPage> SearchAsync(string q, Guid? conversationId, string? cursor, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<PinnedMessageDto>> ListPinsAsync(Guid conversationId, CancellationToken ct = default);
    Task PinAsync(Guid messageId, CancellationToken ct = default);
    Task UnpinAsync(Guid messageId, CancellationToken ct = default);
    Task<IReadOnlyList<FolderDto>> ListFoldersAsync(CancellationToken ct = default);
    Task<FolderDto> CreateFolderAsync(string name, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid folderId, CancellationToken ct = default);
    Task<FolderDto> RenameFolderAsync(Guid folderId, string name, CancellationToken ct = default);
    Task SetFolderItemAsync(Guid folderId, Guid conversationId, bool inFolder, CancellationToken ct = default);
    Task<ConversationSettingsDto> GetSettingsAsync(Guid conversationId, CancellationToken ct = default);
    Task<ConversationSettingsDto> SetDisappearAsync(Guid conversationId, int? seconds, CancellationToken ct = default);
    Task<ConversationSettingsDto> SetE2eeAsync(Guid conversationId, bool enabled, CancellationToken ct = default);
    Task EditAsync(Guid messageId, string content, CancellationToken ct = default);
    Task RecallAsync(Guid messageId, CancellationToken ct = default);
    Task<E2eeKeyDto> RegisterKeyAsync(string publicKeyJwk, CancellationToken ct = default);
    Task RevokeKeyAsync(Guid keyId, CancellationToken ct = default);
    Task<IReadOnlyList<E2eeKeyDto>> GetKeysAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Real-time fan-out of edit/recall/expire events. Uses the existing per-user chat-hub
/// group via INotificationService (event name "message", discriminated by payload.kind) so no
/// Api-side hub changes are required and the worker can publish too.
/// </summary>
public static class ChatEventPublisher
{
    public static Task PublishAsync(INotificationService notify, IEnumerable<Guid> userIds, string kind, object data, CancellationToken ct)
        => notify.PushChatMessageToUsersAsync(userIds, new { kind, data }, ct);
}

public class MessagingPlusService : IMessagingPlusService
{
    public const string E2eePrefix = "e2ee:v1:";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;

    public MessagingPlusService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify)
    {
        _db = db; _current = current; _notify = notify;
    }

    private Guid Uid => _current.UserId ?? throw new ForbiddenException();

    private async Task RequireParticipantAsync(Guid conversationId, CancellationToken ct)
    {
        var uid = Uid;
        if (!await _db.ConversationParticipants.AnyAsync(p => p.ConversationId == conversationId && p.UserId == uid && !p.HasLeft, ct))
            throw new ForbiddenException();
    }

    private Task<List<Guid>> ParticipantIdsAsync(Guid conversationId, CancellationToken ct)
        => _db.ConversationParticipants.Where(p => p.ConversationId == conversationId && !p.HasLeft).Select(p => p.UserId).ToListAsync(ct);

    // ---------- Search ----------

    public async Task<MessageSearchPage> SearchAsync(string q, Guid? conversationId, string? cursor, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid;
        q = (q ?? "").Trim();
        if (q.Length < 2) return new MessageSearchPage(Array.Empty<MessageSearchHitDto>(), null);
        if (q.Length > 100) q = q[..100];

        var myConvs = _db.ConversationParticipants.Where(p => p.UserId == uid && !p.HasLeft).Select(p => p.ConversationId);
        var blockedIds = _db.UserBlocks
            .Where(b => b.BlockerId == uid || b.BlockedUserId == uid)
            .Select(b => b.BlockerId == uid ? b.BlockedUserId : b.BlockerId);

        var escaped = q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var like = $"%{escaped}%";

        var query = _db.Messages.AsNoTracking()
            .Where(m => myConvs.Contains(m.ConversationId) && !m.IsDeletedForAll
                        && m.Content != null && !m.Content.StartsWith(MessagingPlusService.E2eePrefix)
                        && !blockedIds.Contains(m.SenderId)
                        && EF.Functions.ILike(m.Content, like));
        if (conversationId is { } cid) query = query.Where(m => m.ConversationId == cid);

        var cur = MessagingCursor.Decode(cursor);
        if (cur is { } c)
            query = query.Where(m => m.CreatedAt < c.CreatedAt || (m.CreatedAt == c.CreatedAt && m.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(pageSize, 1, 50);
        var rows = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(take + 1)
            .Select(m => new MessageSearchHitDto(m.Id, m.ConversationId, m.SenderId, m.Sender.DisplayName, m.Content, m.CreatedAt))
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = MessagingCursor.Encode(last.CreatedAt, last.MessageId);
            rows.RemoveAt(rows.Count - 1);
        }
        return new MessageSearchPage(rows, next);
    }

    // ---------- Pins ----------

    public async Task<IReadOnlyList<PinnedMessageDto>> ListPinsAsync(Guid conversationId, CancellationToken ct = default)
    {
        await RequireParticipantAsync(conversationId, ct);
        var pins = await _db.Set<ConversationPin>().AsNoTracking()
            .Where(p => p.ConversationId == conversationId).OrderByDescending(p => p.PinnedAt).ToListAsync(ct);
        var ids = pins.Select(p => p.MessageId).ToList();
        var msgs = await _db.Messages.AsNoTracking().Where(m => ids.Contains(m.Id) && !m.IsDeletedForAll)
            .Select(m => new { m.Id, m.Content, m.SenderId, Name = m.Sender.DisplayName }).ToListAsync(ct);
        return pins.Join(msgs, p => p.MessageId, m => m.Id,
            (p, m) => new PinnedMessageDto(m.Id, conversationId, m.Content, m.SenderId, m.Name, p.PinnedBy, p.PinnedAt)).ToList();
    }

    public async Task PinAsync(Guid messageId, CancellationToken ct = default)
    {
        var uid = Uid;
        var m = await _db.Messages.AsNoTracking().Where(x => x.Id == messageId)
            .Select(x => new { x.Id, x.ConversationId, x.IsDeletedForAll }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Message", messageId);
        await RequireParticipantAsync(m.ConversationId, ct);
        if (m.IsDeletedForAll) throw new ConflictException("Message was removed.");
        var pins = _db.Set<ConversationPin>();
        if (await pins.AnyAsync(p => p.ConversationId == m.ConversationId && p.MessageId == messageId, ct)) return;
        if (await pins.CountAsync(p => p.ConversationId == m.ConversationId, ct) >= MessageWindows.MaxPins)
            throw new ConflictException($"A conversation can have at most {MessageWindows.MaxPins} pinned messages.");
        pins.Add(new ConversationPin { ConversationId = m.ConversationId, MessageId = messageId, PinnedBy = uid });
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(m.ConversationId, ct), "pinned",
            new { conversationId = m.ConversationId, messageId, pinned = true }, ct);
    }

    public async Task UnpinAsync(Guid messageId, CancellationToken ct = default)
    {
        var pin = await _db.Set<ConversationPin>().SingleOrDefaultAsync(p => p.MessageId == messageId, ct);
        if (pin is null) return;
        await RequireParticipantAsync(pin.ConversationId, ct);
        _db.Set<ConversationPin>().Remove(pin);
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(pin.ConversationId, ct), "pinned",
            new { conversationId = pin.ConversationId, messageId, pinned = false }, ct);
    }

    // ---------- Folders ----------

    public async Task<IReadOnlyList<FolderDto>> ListFoldersAsync(CancellationToken ct = default)
    {
        var uid = Uid;
        var folders = await _db.Set<ConversationFolder>().AsNoTracking().Where(f => f.UserId == uid).OrderBy(f => f.Name).ToListAsync(ct);
        var ids = folders.Select(f => f.Id).ToList();
        var items = await _db.Set<ConversationFolderItem>().AsNoTracking().Where(i => ids.Contains(i.FolderId)).ToListAsync(ct);
        return folders.Select(f => new FolderDto(f.Id, f.Name, items.Where(i => i.FolderId == f.Id).Select(i => i.ConversationId).ToList())).ToList();
    }

    public async Task<FolderDto> CreateFolderAsync(string name, CancellationToken ct = default)
    {
        var uid = Uid;
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) throw new ConflictException("Folder name must be 1-80 characters.");
        if (await _db.Set<ConversationFolder>().CountAsync(f => f.UserId == uid, ct) >= 20) throw new ConflictException("Folder limit reached.");
        var f = new ConversationFolder { UserId = uid, Name = name };
        _db.Set<ConversationFolder>().Add(f);
        await _db.SaveChangesAsync(ct);
        return new FolderDto(f.Id, f.Name, Array.Empty<Guid>());
    }

    public async Task<FolderDto> RenameFolderAsync(Guid folderId, string name, CancellationToken ct = default)
    {
        var uid = Uid;
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) throw new ConflictException("Folder name must be 1-80 characters.");
        var f = await _db.Set<ConversationFolder>().SingleOrDefaultAsync(x => x.Id == folderId && x.UserId == uid, ct)
            ?? throw new NotFoundException("ConversationFolder", folderId);
        f.Name = name;
        await _db.SaveChangesAsync(ct);
        var ids = await _db.Set<ConversationFolderItem>().AsNoTracking().Where(i => i.FolderId == folderId).Select(i => i.ConversationId).ToListAsync(ct);
        return new FolderDto(f.Id, f.Name, ids);
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken ct = default)
    {
        var uid = Uid;
        var f = await _db.Set<ConversationFolder>().SingleOrDefaultAsync(x => x.Id == folderId && x.UserId == uid, ct)
            ?? throw new NotFoundException("ConversationFolder", folderId);
        _db.Set<ConversationFolder>().Remove(f);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetFolderItemAsync(Guid folderId, Guid conversationId, bool inFolder, CancellationToken ct = default)
    {
        var uid = Uid;
        if (!await _db.Set<ConversationFolder>().AnyAsync(x => x.Id == folderId && x.UserId == uid, ct))
            throw new NotFoundException("ConversationFolder", folderId);
        await RequireParticipantAsync(conversationId, ct);
        var existing = await _db.Set<ConversationFolderItem>().SingleOrDefaultAsync(i => i.FolderId == folderId && i.ConversationId == conversationId, ct);
        if (inFolder && existing is null)
            _db.Set<ConversationFolderItem>().Add(new ConversationFolderItem { FolderId = folderId, ConversationId = conversationId });
        else if (!inFolder && existing is not null)
            _db.Set<ConversationFolderItem>().Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Settings ----------

    private async Task<ConversationSettings> GetOrCreateSettingsAsync(Guid conversationId, CancellationToken ct)
    {
        var s = await _db.Set<ConversationSettings>().SingleOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        if (s is null)
        {
            s = new ConversationSettings { ConversationId = conversationId };
            _db.Set<ConversationSettings>().Add(s);
        }
        return s;
    }

    private static ConversationSettingsDto ToDto(ConversationSettings s)
        => new(s.ConversationId, s.DisappearAfterSeconds, s.MutedUntil, s.E2eeEnabled);

    public async Task<ConversationSettingsDto> GetSettingsAsync(Guid conversationId, CancellationToken ct = default)
    {
        await RequireParticipantAsync(conversationId, ct);
        var s = await _db.Set<ConversationSettings>().AsNoTracking().SingleOrDefaultAsync(x => x.ConversationId == conversationId, ct);
        return s is null ? new ConversationSettingsDto(conversationId, null, null, false) : ToDto(s);
    }

    public async Task<ConversationSettingsDto> SetDisappearAsync(Guid conversationId, int? seconds, CancellationToken ct = default)
    {
        await RequireParticipantAsync(conversationId, ct);
        if (!MessageWindows.IsValidDisappear(seconds)) throw new ConflictException("Unsupported disappearing-message duration.");
        var s = await GetOrCreateSettingsAsync(conversationId, ct);
        s.DisappearAfterSeconds = seconds;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(conversationId, ct), "settings",
            new { conversationId, disappearAfterSeconds = seconds, e2eeEnabled = s.E2eeEnabled }, ct);
        return ToDto(s);
    }

    public async Task<ConversationSettingsDto> SetE2eeAsync(Guid conversationId, bool enabled, CancellationToken ct = default)
    {
        await RequireParticipantAsync(conversationId, ct);
        var s = await GetOrCreateSettingsAsync(conversationId, ct);
        s.E2eeEnabled = enabled;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(conversationId, ct), "settings",
            new { conversationId, disappearAfterSeconds = s.DisappearAfterSeconds, e2eeEnabled = enabled }, ct);
        return ToDto(s);
    }

    // ---------- Edit / recall ----------

    public async Task EditAsync(Guid messageId, string content, CancellationToken ct = default)
    {
        var uid = Uid;
        content = (content ?? "").Trim();
        if (content.Length is < 1 or > 4000) throw new ConflictException("Message must be 1-4000 characters.");
        var m = await _db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct) ?? throw new NotFoundException("Message", messageId);
        if (m.SenderId != uid) throw new ForbiddenException();
        if (m.IsDeletedForAll) throw new ConflictException("Message was removed.");
        if (m.Type != MessageType.Text) throw new ConflictException("Only text messages can be edited.");
        if (!MessageWindows.CanEdit(m.CreatedAt, DateTime.UtcNow)) throw new ConflictException("Messages can only be edited within 15 minutes.");

        var e2ee = await _db.Set<ConversationSettings>().AnyAsync(s => s.ConversationId == m.ConversationId && s.E2eeEnabled, ct);
        if (e2ee && !content.StartsWith(E2eePrefix)) throw new ConflictException("This conversation is end-to-end encrypted; send an encrypted envelope.");

        _db.Set<MessageEdit>().Add(new MessageEdit { MessageId = m.Id, PreviousContent = m.Content });
        m.Content = content;
        m.IsEdited = true;
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(m.ConversationId, ct), "edited",
            new { messageId = m.Id, conversationId = m.ConversationId, content, isEdited = true }, ct);
    }

    public async Task RecallAsync(Guid messageId, CancellationToken ct = default)
    {
        var uid = Uid;
        var m = await _db.Messages.SingleOrDefaultAsync(x => x.Id == messageId, ct) ?? throw new NotFoundException("Message", messageId);
        if (m.SenderId != uid) throw new ForbiddenException();
        if (m.IsDeletedForAll) return;
        if (!MessageWindows.CanRecall(m.CreatedAt, DateTime.UtcNow)) throw new ConflictException("Messages can only be recalled within 10 minutes.");
        // Same semantics as DeleteMessageAsync: tombstone, content and media cleared.
        m.IsDeletedForAll = true;
        m.Content = null;
        m.MediaUrl = null;
        var pins = await _db.Set<ConversationPin>().Where(p => p.MessageId == messageId).ToListAsync(ct);
        _db.Set<ConversationPin>().RemoveRange(pins);
        await _db.SaveChangesAsync(ct);
        await ChatEventPublisher.PublishAsync(_notify, await ParticipantIdsAsync(m.ConversationId, ct), "removed",
            new { messageId = m.Id, conversationId = m.ConversationId, reason = "recalled" }, ct);
    }

    // ---------- E2EE key registry ----------

    public async Task<E2eeKeyDto> RegisterKeyAsync(string publicKeyJwk, CancellationToken ct = default)
    {
        var uid = Uid;
        if (string.IsNullOrWhiteSpace(publicKeyJwk) || publicKeyJwk.Length > 2000) throw new ConflictException("Invalid public key.");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(publicKeyJwk);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object
                || !root.TryGetProperty("kty", out _) || !root.TryGetProperty("x", out _) || !root.TryGetProperty("y", out _)
                || root.TryGetProperty("d", out _))
                throw new ConflictException("Invalid public key (must be an EC public JWK without private parts).");
        }
        catch (System.Text.Json.JsonException) { throw new ConflictException("Invalid public key."); }

        var now = DateTime.UtcNow;
        var active = await _db.Set<UserE2eeKey>().Where(k => k.UserId == uid && k.RevokedAt == null).ToListAsync(ct);
        foreach (var k in active) k.RevokedAt = now; // one active key per user (single-device scope)
        var key = new UserE2eeKey { UserId = uid, PublicKeyJwk = publicKeyJwk };
        _db.Set<UserE2eeKey>().Add(key);
        await _db.SaveChangesAsync(ct);
        return new E2eeKeyDto(key.Id, uid, key.PublicKeyJwk, key.CreatedAt);
    }

    public async Task RevokeKeyAsync(Guid keyId, CancellationToken ct = default)
    {
        var uid = Uid;
        var k = await _db.Set<UserE2eeKey>().SingleOrDefaultAsync(x => x.Id == keyId && x.UserId == uid, ct)
            ?? throw new NotFoundException("UserE2eeKey", keyId);
        k.RevokedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<E2eeKeyDto>> GetKeysAsync(Guid userId, CancellationToken ct = default)
    {
        _ = Uid;
        return await _db.Set<UserE2eeKey>().AsNoTracking().Where(k => k.UserId == userId && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt).Select(k => new E2eeKeyDto(k.Id, k.UserId, k.PublicKeyJwk, k.CreatedAt)).ToListAsync(ct);
    }
}

/// <summary>Send-time spam controls, called from MessagingService.</summary>
public interface IMessageSpamGuard
{
    Task EnforceNewConversationLimitAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Throws on Block; returns a flag reason (or null) for Flag.</summary>
    Task<string?> CheckOutgoingAsync(Guid userId, Guid conversationId, string? content, CancellationToken ct = default);
    Task AutoFlagAsync(Guid messageId, Guid senderId, string reason, CancellationToken ct = default);
}

public class MessageSpamGuard : IMessageSpamGuard
{
    private readonly ApplicationDbContext _db;
    private readonly IRedisRateLimiter _limiter;
    public const int NewConversationsPerHour = 10;

    public MessageSpamGuard(ApplicationDbContext db, IRedisRateLimiter limiter) { _db = db; _limiter = limiter; }

    public async Task EnforceNewConversationLimitAsync(Guid userId, CancellationToken ct = default)
    {
        RateLimitDecision d;
        try { d = await _limiter.CheckAsync("msg:new-conversation", userId.ToString(), NewConversationsPerHour, TimeSpan.FromHours(1), ct); }
        catch { return; } // fail open if Redis is down
        if (!d.Allowed)
            throw new AppException("rate_limited", $"You are starting conversations too quickly. Try again in {Math.Ceiling(d.ResetIn.TotalMinutes)} minute(s).");
    }

    public async Task<string?> CheckOutgoingAsync(Guid userId, Guid conversationId, string? content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content) || content.StartsWith(MessagingPlusService.E2eePrefix)) return null;
        var since = DateTime.UtcNow - SpamHeuristics.DuplicateWindow;
        var trimmed = content.Trim();
        var identical = await _db.Messages.AsNoTracking()
            .CountAsync(m => m.SenderId == userId && m.CreatedAt >= since && m.Content != null && m.Content == trimmed, ct);
        var createdAt = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.CreatedAt).SingleOrDefaultAsync(ct);
        var isNew = createdAt != default && DateTime.UtcNow - createdAt < TimeSpan.FromDays(2);
        var a = SpamHeuristics.Assess(content, identical, isNew);
        if (a.Verdict == SpamVerdict.Block) throw new AppException("spam_blocked", a.Reason ?? "Message blocked as spam.");
        return a.Verdict == SpamVerdict.Flag ? a.Reason : null;
    }

    public async Task AutoFlagAsync(Guid messageId, Guid senderId, string reason, CancellationToken ct = default)
    {
        // Reporter is the sender (no system user exists); Details marks it as automated so moderators can filter.
        _db.MessageReports.Add(new MessageReport { MessageId = messageId, ReporterId = senderId, Reason = "[auto] " + reason });
        _db.ContentReports.Add(new ContentReport
        {
            TargetType = ReportTargetType.Message, TargetId = messageId, ReporterId = senderId,
            Reason = "spam", Details = "[auto-flag] " + reason
        });
        await _db.SaveChangesAsync(ct);
    }
}

/// <summary>Coalesces per-conversation unread-message notifications inside a short window.</summary>
public class MessageNotificationCoalescer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly ILogger<MessageNotificationCoalescer> _logger;

    public MessageNotificationCoalescer(IConnectionMultiplexer redis, ApplicationDbContext db, INotificationService notify,
        ILogger<MessageNotificationCoalescer> logger)
    { _redis = redis; _db = db; _notify = notify; _logger = logger; }

    public async Task NotifyAsync(Guid userId, Guid conversationId, Guid messageId, string preview, CancellationToken ct = default)
    {
        long count = 1;
        try
        {
            var key = $"msgnotif:{userId}:{conversationId}";
            var db = _redis.GetDatabase();
            count = await db.StringIncrementAsync(key);
            if (count == 1) await db.KeyExpireAsync(key, NotificationCoalescing.Window);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "coalescer redis unavailable; sending uncoalesced"); }

        if (count > 1)
        {
            var needle = conversationId.ToString();
            var since = DateTime.UtcNow - NotificationCoalescing.Window;
            var existing = await _db.Notifications
                .Where(n => n.UserId == userId && n.Title == "New message" && !n.IsRead && n.CreatedAt >= since
                            && n.Payload != null && n.Payload.Contains(needle))
                .OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                existing.Body = NotificationCoalescing.Body((int)count, preview);
                existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return;
            }
            // Original not persisted yet (async worker) or already read: fall through and send a fresh one.
        }
        await _notify.NotifyUserAsync(userId, "New message", preview, new { conversationId, messageId }, ct);
    }
}
