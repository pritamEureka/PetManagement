using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// JSON-friendly Redis primitives + read-through and counter helpers.
///
/// Resilience: every call catches RedisExceptions and logs at Warning; the
/// helper returns <c>default(T)</c>, so callers can always fall back to the
/// underlying source of truth. The decision to swallow lives here, not in
/// callers — caches should *never* turn outages into 500s.
/// </summary>
public class CacheHelper
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CacheHelper> _logger;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CacheHelper(IConnectionMultiplexer redis, ILogger<CacheHelper> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public IDatabase Db => _redis.GetDatabase();
    public IConnectionMultiplexer Multiplexer => _redis;

    // ---- Strings ------------------------------------------------------------

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var v = await Db.StringGetAsync(key);
            return v.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(v!, Json);
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis GET failed for {Key}", key); return default; }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, Json);
            return await Db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis SET failed for {Key}", key); return false; }
    }

    public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, Json);
            return await Db.StringSetAsync(key, json, ttl, When.NotExists);
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis SET NX failed for {Key}", key); return false; }
    }

    public async Task RemoveAsync(string key)
    {
        try { await Db.KeyDeleteAsync(key); }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis DEL failed for {Key}", key); }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try { return await Db.KeyExistsAsync(key); }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis EXISTS failed for {Key}", key); return false; }
    }

    /// <summary>Read-through: returns cached value or hydrates via <paramref name="loader"/> and caches the result.</summary>
    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> loader, TimeSpan ttl)
    {
        var cached = await GetAsync<T>(key);
        if (cached is not null) return cached;
        var fresh = await loader();
        if (fresh is not null) await SetAsync(key, fresh, ttl);
        return fresh;
    }

    // ---- Counters -----------------------------------------------------------

    public async Task<long> IncrementAsync(string key, long delta = 1, TimeSpan? ttl = null)
    {
        try
        {
            var v = await Db.StringIncrementAsync(key, delta);
            if (ttl.HasValue) await Db.KeyExpireAsync(key, ttl.Value, ExpireWhen.HasNoExpiry);
            return v;
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis INCR failed for {Key}", key); return 0; }
    }

    public async Task<long?> GetCounterAsync(string key)
    {
        try
        {
            var v = await Db.StringGetAsync(key);
            return v.IsNullOrEmpty ? null : (long)v;
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis GET counter failed for {Key}", key); return null; }
    }

    // ---- Pattern eviction ---------------------------------------------------

    /// <summary>
    /// Best-effort: iterates every server's keyspace and deletes matching keys.
    /// Production tip: prefer a version-key (bump-to-invalidate) over SCAN+DEL
    /// when the keyspace gets large — see <see cref="Modules.Marketplace.MarketplaceCache"/>.
    /// </summary>
    public async Task RemoveByPatternAsync(string pattern)
    {
        try
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica) continue;
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0) await Db.KeyDeleteAsync(keys);
            }
        }
        catch (RedisException ex) { _logger.LogWarning(ex, "Redis SCAN/DEL failed for {Pattern}", pattern); }
    }
}
