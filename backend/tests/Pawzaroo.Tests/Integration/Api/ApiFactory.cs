using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Pawzaroo.Application.Common.Interfaces;
using Xunit;

namespace Pawzaroo.Tests.Integration.Api;

/// <summary>
/// Boots the real API (Program.cs) in-process against a throwaway Postgres container.
///
/// Notes for maintainers:
///  - <c>Program</c> is internal (top-level statements, no InternalsVisibleTo on the Api project), so a PUBLIC type of the
///    Api assembly (AuthController) is used as the entry-point anchor. WebApplicationFactory only needs the assembly.
///  - Program.cs reads configuration eagerly while composing services, so configuration is supplied through
///    process environment variables (set once, before the host is built) instead of ConfigureAppConfiguration.
///  - Kafka producer is replaced with a no-op and the in-proc Kafka consumer hosted service is removed.
///  - Redis is NOT replaced: the API uses it for caches, SignalR backplane and rate limiting. Tests that need the real
///    host are marked <see cref="RedisFactAttribute"/> and skip when localhost:6379 is unreachable.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Pawzaroo.Api.Controllers.V1.AuthController>, IAsyncLifetime
{
    public const string SuperAdminEmail = "api-it-superadmin@pawzaroo.test";
    public const string SuperAdminPassword = "Sup3rAdmin!Passw0rd-IT";
    public const string CourierSecret = "api-it-courier-secret-0123456789";

    private readonly PostgresFixture _pg = new();

    public async Task InitializeAsync()
    {
        await _pg.InitializeAsync();   // container + EnsureCreated on pawzaroo_test (unused by the API)

        // Dedicated database so the API's own MigrateAsync starts from an empty schema.
        var baseCs = new NpgsqlConnectionStringBuilder(_pg.Db.Database.GetConnectionString()!);
        await using (var admin = new NpgsqlConnection(baseCs.ConnectionString))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand("CREATE DATABASE api_it", admin);
            await cmd.ExecuteNonQueryAsync();
        }
        baseCs.Database = "api_it";

        Set("ConnectionStrings__Postgres", baseCs.ConnectionString);
        Set("ConnectionStrings__Redis", RedisFactAttribute.ConnectionString);
        Set("Jwt__SigningKey", "api-it-signing-key-0123456789-0123456789-abcdef");
        Set("Kafka__BootstrapServers", "127.0.0.1:1");           // unreachable on purpose; producer is replaced anyway
        Set("Storage__Endpoint", "http://127.0.0.1:1");
        Set("Storage__AccessKey", "it-access-key");
        Set("Storage__SecretKey", "it-secret-key");
        Set("SuperAdmin__Email", SuperAdminEmail);
        Set("SuperAdmin__Password", SuperAdminPassword);
        Set("Database__AutoMigrate", "true");
        Set("RateLimiting__AuthPermitPerMinute", "10000");   // the suite makes many /auth calls from one client
        Set("Courier__WebhookSecret", CourierSecret);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private static void Set(string k, string v) => Environment.SetEnvironmentVariable(k, v);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IKafkaProducer>();
            services.AddSingleton<IKafkaProducer, NoopKafkaProducer>();

            // No background workers in API tests (Kafka consumer is the only API-side hosted service today).
            foreach (var d in services.Where(d => d.ServiceType == typeof(IHostedService)
                         && (d.ImplementationType?.Namespace?.StartsWith("Pawzaroo") ?? false)).ToList())
                services.Remove(d);

            // Test-only controller (idempotency probe) lives in this assembly.
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            // The app uses AddControllersAsServices(), which registers only the controllers known at that moment.
            services.AddTransient<IdempotencyProbeController>();
        });
    }

    private sealed class NoopKafkaProducer : IKafkaProducer
    {
        public Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

/// <summary>[Fact] that is skipped (not failed) when Redis is unreachable. Tag: Trait("Requires","Redis").</summary>
public sealed class RedisFactAttribute : FactAttribute
{
    public const string ConnectionString = "localhost:6379,abortConnect=false";

    public RedisFactAttribute()
    {
        if (!Reachable()) Skip = "Redis is not reachable on localhost:6379 (start `docker compose up redis`).";
    }

    private static readonly Lazy<bool> Probe = new(() =>
    {
        try
        {
            using var c = new TcpClient();
            return c.ConnectAsync("127.0.0.1", 6379).Wait(TimeSpan.FromMilliseconds(750)) && c.Connected;
        }
        catch { return false; }
    });

    public static bool IsReachable => Probe.Value;
    private static bool Reachable() => Probe.Value;
}

/// <summary>[Theory] that is skipped when Redis is unreachable.</summary>
public sealed class RedisTheoryAttribute : TheoryAttribute
{
    public RedisTheoryAttribute()
    {
        if (!RedisFactAttribute.IsReachable) Skip = "Redis is not reachable on localhost:6379 (start `docker compose up redis`).";
    }
}
