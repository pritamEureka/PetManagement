using Serilog.Context;

namespace Pawzaroo.Api.Middleware;

/// <summary>
/// Per-request correlation ID: respect inbound X-Correlation-Id, else generate;
/// echo on the response, attach to Serilog scope, expose to handlers via ctx.Items.
/// </summary>
public class RequestLoggingMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var cid = ctx.Request.Headers.TryGetValue(HeaderName, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Items["CorrelationId"] = cid;
        ctx.Response.Headers[HeaderName] = cid;

        using (LogContext.PushProperty("CorrelationId", cid))
        using (LogContext.PushProperty("User", ctx.User.Identity?.Name ?? "anon"))
        {
            await _next(ctx);
        }
    }
}
