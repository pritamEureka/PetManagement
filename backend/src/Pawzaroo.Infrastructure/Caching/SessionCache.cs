using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Token/session caches:
///   - Blacklist: short-lived access-token jti -&gt; revoked flag. Checked by
///     the JWT middleware on every request — keep the lookup O(1) and local.
///   - Refresh tracking: persists jti -&gt; userId so refresh-token rotation
///     can revoke an entire session without a DB round-trip.
/// </summary>
public class SessionCache : ISessionCache
{
    private readonly CacheHelper _cache;
    public SessionCache(CacheHelper cache) => _cache = cache;

    // --- Access-token blacklist --------------------------------------------

    public async Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        // Use raw string "1" to keep the key tiny — JSON-encoding adds quoting we don't need.
        try { await _cache.Db.StringSetAsync(RedisKeys.Blacklist(jti), "1", ttl); }
        catch (RedisException) { /* swallow: blacklist is best-effort */ }
    }

    public Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken ct = default)
        => _cache.ExistsAsync(RedisKeys.Blacklist(jti));

    // --- Refresh-token sessions --------------------------------------------

    public Task TrackRefreshAsync(string refreshJti, Guid userId, TimeSpan ttl, CancellationToken ct = default)
        => _cache.SetAsync(RedisKeys.Session(refreshJti), userId, ttl);

    public async Task<Guid?> ResolveRefreshAsync(string refreshJti, CancellationToken ct = default)
    {
        var v = await _cache.GetAsync<Guid?>(RedisKeys.Session(refreshJti));
        return v;
    }

    public Task RevokeRefreshAsync(string refreshJti, CancellationToken ct = default)
        => _cache.RemoveAsync(RedisKeys.Session(refreshJti));
}
