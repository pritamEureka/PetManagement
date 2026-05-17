using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Append-only audit of every privileged action the API mutates. Used by:
///   - admin services to record approvals, suspensions, refunds, role grants
///   - the admin UI to display action history
/// Reads require <c>audit.view</c>; writes are open to any service code.
/// </summary>
public class AdminActionLogger : IAdminActionLogger
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IHttpContextAccessor? _http;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Field-name patterns we always redact before persisting payloads.
    private static readonly string[] SensitiveKeys =
        { "password", "passwordHash", "token", "refreshToken", "secret", "signingKey", "code" };

    public AdminActionLogger(ApplicationDbContext db, ICurrentUserService current, IHttpContextAccessor? http = null)
    {
        _db = db;
        _current = current;
        _http = http;
    }

    public async Task LogAsync(string action, string targetType, string? targetId,
        string? reason, object? payload, CancellationToken ct = default)
    {
        var adminId = _current.UserId;
        if (adminId is null) return; // background services log via IAuditLogger instead

        string? payloadJson = null;
        if (payload is not null)
            payloadJson = JsonSerializer.Serialize(Redact(payload), Json);

        _db.AdminActionLogs.Add(new AdminActionLog
        {
            AdminId = adminId.Value,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason,
            PayloadJson = payloadJson,
            IpAddress = _http?.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _http?.HttpContext?.Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AdminActionLogDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Audit.View)) throw new ForbiddenException();
        return await _db.AdminActionLogs.AsNoTracking()
            .OrderByDescending(l => l.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new AdminActionLogDto(l.Id, l.At, l.AdminId,
                l.Admin.DisplayName, l.Action, l.TargetType, l.TargetId, l.Reason, l.IpAddress))
            .ToListAsync(ct);
    }

    // Crude but effective: walk an object via JsonNode-ish reflection through
    // System.Text.Json, then null out fields whose name matches a sensitive key.
    private static object Redact(object payload)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(payload, Json);
            if (node is null) return payload;
            RedactNode(node);
            return node;
        }
        catch { return payload; }
    }

    private static void RedactNode(System.Text.Json.Nodes.JsonNode node)
    {
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            foreach (var kv in obj.ToArray())
            {
                if (SensitiveKeys.Any(s => kv.Key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                    obj[kv.Key] = "***";
                else if (kv.Value is not null) RedactNode(kv.Value);
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray arr)
        {
            foreach (var child in arr.OfType<System.Text.Json.Nodes.JsonNode>()) RedactNode(child);
        }
    }
}
