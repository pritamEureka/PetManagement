using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pawzaroo.Infrastructure.Observability;

/// <summary>
/// JSON writer for <c>HealthCheckOptions.ResponseWriter</c>:
/// <c>{ status, version, gitSha, totalDurationMs, checks: [{ name, status, durationMs, description }] }</c>.
/// Exception details are never exposed.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(Serialize(report));
    }

    public static string Serialize(HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            version = BuildInfo.Version,
            gitSha = BuildInfo.GitSha,
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                description = e.Value.Description
            }).ToArray()
        };
        return JsonSerializer.Serialize(body, Json);
    }
}
