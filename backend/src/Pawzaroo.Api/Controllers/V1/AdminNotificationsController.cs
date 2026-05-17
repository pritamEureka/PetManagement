using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Admin broadcast + outbox view. Listing is permission-gated; sending fans out
/// through INotificationService which already handles SignalR + Kafka.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/notifications")]
[Authorize]
public class AdminNotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;

    public AdminNotificationsController(ApplicationDbContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    public record NotificationRow(
        Guid Id, Guid UserId, string UserDisplayName,
        string Title, string Body, bool IsRead, DateTime CreatedAt);

    [HttpGet]
    [Permission(Permissions.Notifications.View)]
    public async Task<IReadOnlyList<NotificationRow>> List(
        [FromQuery] Guid? userId,
        [FromQuery] bool? unreadOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var q = _db.Notifications.AsNoTracking().AsQueryable();
        if (userId.HasValue)    q = q.Where(n => n.UserId == userId);
        if (unreadOnly == true) q = q.Where(n => !n.IsRead);
        return await q.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationRow(n.Id, n.UserId, n.User.DisplayName,
                n.Title, n.Body, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public record BroadcastBody(string Title, string Body, object? Payload);

    [HttpPost("broadcast")]
    [Permission(Permissions.Notifications.Create)]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastBody body, CancellationToken ct)
    {
        await _notify.BroadcastAsync(body.Title, body.Body, body.Payload, ct);
        return NoContent();
    }

    public record DirectBody(Guid UserId, string Title, string Body, object? Payload);

    [HttpPost("send")]
    [Permission(Permissions.Notifications.Create)]
    public async Task<IActionResult> Send([FromBody] DirectBody body, CancellationToken ct)
    {
        await _notify.NotifyUserAsync(body.UserId, body.Title, body.Body, body.Payload, ct);
        return NoContent();
    }
}
