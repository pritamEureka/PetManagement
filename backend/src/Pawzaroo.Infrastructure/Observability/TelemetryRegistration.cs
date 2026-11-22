using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Pawzaroo.Infrastructure.Observability;

public static class TelemetryRegistration
{
    /// <summary>
    /// Registers OpenTelemetry tracing + metrics for the "Pawzaroo" source/meter plus ASP.NET Core, HttpClient
    /// and runtime instrumentation. Exports over OTLP only when <c>OpenTelemetry:Endpoint</c> is configured
    /// (optional <c>OpenTelemetry:Protocol</c> = grpc | http, <c>OpenTelemetry:ServiceName</c>).
    /// </summary>
    public static IServiceCollection AddPawzarooTelemetry(this IServiceCollection services, IConfiguration config)
    {
        var serviceName = config["OpenTelemetry:ServiceName"]
                          ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name
                          ?? "pawzaroo";

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: BuildInfo.Version)
                .AddAttributes(new KeyValuePair<string, object>[] { new("git.sha", BuildInfo.GitSha) }))
            .WithTracing(t => t
                .AddSource(PawzarooTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
                .AddMeter(PawzarooTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        var endpoint = config["OpenTelemetry:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var http = string.Equals(config["OpenTelemetry:Protocol"], "http", StringComparison.OrdinalIgnoreCase);
            otel.UseOtlpExporter(http ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc, uri);
        }

        return services;
    }
}
