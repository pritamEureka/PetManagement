using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public AuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    [Permission(Permissions.Reports.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? entityName, [FromQuery] string? action,
        [FromQuery] Guid? userId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var q = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityName)) q = q.Where(a => a.EntityName == entityName);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (userId.HasValue) q = q.Where(a => a.UserId == userId);
        if (from.HasValue) q = q.Where(a => a.At >= from);
        if (to.HasValue) q = q.Where(a => a.At < to);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return Ok(new { total, items });
    }
}
