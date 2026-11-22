using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Observability;

public record RedisCapacity(
    bool Available, long UsedMemoryBytes, long MaxMemoryBytes, double? MemoryUsedPercent,
    long ConnectedClients, long? MaxClients, long TotalCommandsProcessed, long EvictedKeys, string? Error);

public record PostgresCapacity(
    bool Available, long? DatabaseSizeBytes, int Connections, int? MaxConnections, double? ConnectionsUsedPercent,
    int ActiveConnections, int IdleInTransaction, long XactCommit, long XactRollback,
    double? CacheHitRatio, long Deadlocks, string? Error);

public record InfrastructureCapacity(DateTime At, string Version, string GitSha, RedisCapacity Redis, PostgresCapacity Postgres);

public interface IInfrastructureCapacityService
{
    Task<InfrastructureCapacity> GetAsync(CancellationToken ct = default);
}

/// <summary>Reads Redis INFO and Postgres pg_stat_* views. Each probe fails independently.</summary>
public class InfrastructureCapacityService : IInfrastructureCapacityService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<InfrastructureCapacityService> _logger;

    public InfrastructureCapacityService(IConnectionMultiplexer redis, ApplicationDbContext db,
        ILogger<InfrastructureCapacityService> logger)
    {
        _redis = redis;
        _db = db;
        _logger = logger;
    }

    public async Task<InfrastructureCapacity> GetAsync(CancellationToken ct = default)
        => new(DateTime.UtcNow, BuildInfo.Version, BuildInfo.GitSha, await ProbeRedisAsync(), await ProbePostgresAsync(ct));

    private async Task<RedisCapacity> ProbeRedisAsync()
    {
        try
        {
            var server = _redis.GetServers().FirstOrDefault(s => s.IsConnected && !s.IsReplica)
                         ?? _redis.GetServers().FirstOrDefault(s => s.IsConnected)
                         ?? throw new InvalidOperationException("No connected Redis server");

            var info = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in (await server.InfoAsync()).SelectMany(g => g)) info[kv.Key] = kv.Value;
            long L(string k) => info.TryGetValue(k, out var v) && long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

            var used = L("used_memory");
            var max = L("maxmemory");
            long? maxClients = null;
            try
            {
                var cfg = await server.ConfigGetAsync("maxclients");
                if (cfg.Length > 0 && long.TryParse(cfg[0].Value, out var mc)) maxClients = mc;
            }
            catch (RedisException) { /* CONFIG often disabled on managed Redis */ }

            return new RedisCapacity(true, used, max, max > 0 ? Math.Round(used * 100.0 / max, 1) : null,
                L("connected_clients"), maxClients, L("total_commands_processed"), L("evicted_keys"), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[capacity] redis probe failed");
            return new RedisCapacity(false, 0, 0, null, 0, null, 0, 0, ex.Message);
        }
    }

    private async Task<PostgresCapacity> ProbePostgresAsync(CancellationToken ct)
    {
        try
        {
            var conn = _db.Database.GetDbConnection();
            var wasClosed = conn.State != ConnectionState.Open;
            if (wasClosed) await conn.OpenAsync(ct);
            try
            {
                long size = 0, commit = 0, rollback = 0, hit = 0, read = 0, deadlocks = 0;
                int backends = 0;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT numbackends, xact_commit, xact_rollback, blks_hit, blks_read, deadlocks, pg_database_size(datname) " +
                                      "FROM pg_stat_database WHERE datname = current_database()";
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct))
                    {
                        backends  = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                        commit    = Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture);
                        rollback  = Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture);
                        hit       = Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture);
                        read      = Convert.ToInt64(r.GetValue(4), CultureInfo.InvariantCulture);
                        deadlocks = Convert.ToInt64(r.GetValue(5), CultureInfo.InvariantCulture);
                        size      = Convert.ToInt64(r.GetValue(6), CultureInfo.InvariantCulture);
                    }
                }

                int active = 0, idleInTx = 0;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT state, count(*) FROM pg_stat_activity WHERE datname = current_database() GROUP BY state";
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    while (await r.ReadAsync(ct))
                    {
                        var state = r.IsDBNull(0) ? "" : r.GetString(0);
                        var n = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                        if (state == "active") active = n;
                        else if (state.StartsWith("idle in transaction", StringComparison.Ordinal)) idleInTx += n;
                    }
                }

                int? maxConn = null;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT setting::int FROM pg_settings WHERE name = 'max_connections'";
                    var v = await cmd.ExecuteScalarAsync(ct);
                    if (v is not null and not DBNull) maxConn = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                }

                var total = hit + read;
                return new PostgresCapacity(true, size, backends, maxConn,
                    maxConn is > 0 ? Math.Round(backends * 100.0 / maxConn.Value, 1) : null,
                    active, idleInTx, commit, rollback,
                    total > 0 ? Math.Round(hit * 100.0 / total, 2) : null, deadlocks, null);
            }
            finally
            {
                if (wasClosed) await conn.CloseAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[capacity] postgres probe failed");
            return new PostgresCapacity(false, null, 0, null, null, 0, 0, 0, 0, null, 0, ex.Message);
        }
    }
}
