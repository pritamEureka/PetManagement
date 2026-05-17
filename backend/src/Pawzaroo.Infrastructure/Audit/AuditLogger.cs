using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Audit;

public class AuditLogger : IAuditLogger
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IHttpContextAccessor _http;

    public AuditLogger(ApplicationDbContext db, ICurrentUserService current, IHttpContextAccessor http)
    {
        _db = db;
        _current = current;
        _http = http;
    }

    public async Task LogAsync(string action, string entityName, string? entityId, string? module = null,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default)
    {
        var entry = new AuditEntry
        {
            UserId = _current.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Module = module,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _http.HttpContext?.Request.Headers.UserAgent.ToString(),
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues)
        };
        _db.AuditEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
