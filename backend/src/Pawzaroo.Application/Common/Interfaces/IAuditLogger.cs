namespace Pawzaroo.Application.Common.Interfaces;

public interface IAuditLogger
{
    Task LogAsync(string action, string entityName, string? entityId, string? module = null,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default);
}
