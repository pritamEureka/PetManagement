using Microsoft.AspNetCore.Mvc.Filters;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Api.Filters;

/// <summary>
/// Audits any action explicitly decorated with [Audit("posts","create")].
/// Persists *after* the action completes successfully so failed writes do
/// not pollute the audit trail.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AuditAttribute : Attribute
{
    public string Module { get; }
    public string Action { get; }
    public string? EntityName { get; }
    public string? EntityIdRouteKey { get; }
    public AuditAttribute(string module, string action, string? entityName = null, string? entityIdRouteKey = "id")
    { Module = module; Action = action; EntityName = entityName; EntityIdRouteKey = entityIdRouteKey; }
}

public class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditLogger _audit;
    public AuditActionFilter(IAuditLogger audit) => _audit = audit;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null) return;

        var attr = context.ActionDescriptor.EndpointMetadata.OfType<AuditAttribute>().FirstOrDefault();
        if (attr is null) return;

        string? entityId = null;
        if (attr.EntityIdRouteKey is { } key && context.RouteData.Values.TryGetValue(key, out var v))
            entityId = v?.ToString();

        await _audit.LogAsync(
            action: attr.Action,
            entityName: attr.EntityName ?? context.Controller.GetType().Name,
            entityId: entityId,
            module: attr.Module);
    }
}
