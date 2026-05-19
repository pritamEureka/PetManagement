using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// User-facing notifications: list own notifications, get unread count, and mark as read.
///
/// Unread count reads come from a Redis counter maintained by
/// <c>NotificationCountCache</c> (bumped by the notification producer, reset on
/// mark-all-read). DB COUNT(*) only runs as a cold-start hydration fallback —
/// hot path is sub-millisecond.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationCountCache _counter;

    public NotificationsController(ApplicationDbContext db, ICurrentUserService current,
        INotificationCountCache counter)
    {
        _db = db;
        _current = current;
        _counter = counter;
    }

    public record NotificationDto(
        Guid Id, string Title, string Body, string? Payload, string? Url,
        bool IsRead, DateTime CreatedAt, DateTime? ReadAt);

    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> List(
        [FromQuery] bool? unreadOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var uid = _current.UserId!.Value;
        var q = _db.Notifications.AsNoTracking().Where(n => n.UserId == uid);
        if (unreadOnly == true) q = q.Where(n => !n.IsRead);

        return await q.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Body, n.Payload, n.Url,
                n.IsRead, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
    }

    public record UnreadCountDto(int Count);

    [HttpGet("unread-count")]
    public async Task<UnreadCountDto> UnreadCount(CancellationToken ct)
    {
        var uid = _current.UserId!.Value;

        // Fast path: Redis counter. If the key is unset (cold cache, fresh user,
        // TTL eviction) the counter returns 0 — we hydrate from the DB and
        // backfill so subsequent reads stay on the cache.
        var cached = await _counter.GetUnreadAsync(uid, ct);
        if (cached > 0) return new UnreadCountDto((int)cached);

        var dbCount = await _db.Notifications.CountAsync(n => n.UserId == uid && !n.IsRead, ct);
        if (dbCount > 0) await _counter.BumpUnreadAsync(uid, dbCount, ct);
        return new UnreadCountDto(dbCount);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var uid = _current.UserId!.Value;
        var notification = await _db.Notifications
            .SingleOrDefaultAsync(n => n.Id == id && n.UserId == uid, ct);
        if (notification is null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _counter.DecrementUnreadAsync(uid, 1, ct);
        }
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var uid = _current.UserId!.Value;
        await _db.Notifications
            .Where(n => n.UserId == uid && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        await _counter.ResetUnreadAsync(uid, ct);
        return NoContent();
    }
}
