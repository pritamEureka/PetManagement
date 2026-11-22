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
    private readonly IDegradedModePolicy _policy;

    public SessionCache(CacheHelper cache, IDegradedModePolicy? policy = null)
    {
        _cache = cache;
        _policy = policy ?? new DegradedModePolicyService();
    }

    // --- Access-token blacklist --------------------------------------------

    public async Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        // Use raw string "1" to keep the key tiny — JSON-encoding adds quoting we don't need.
        try { await _cache.Db.StringSetAsync(RedisKeys.Blacklist(jti), "1", ttl); }
        catch (RedisException) { /* swallow: blacklist is best-effort */ }
    }

    public async Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken ct = default)
    {
        try { return await _cache.Db.KeyExistsAsync(RedisKeys.Blacklist(jti)); }
        catch (RedisException ex)
        {
            // Policy-driven (Security:RedisDegraded:TokenBlacklist). Default FailOpen: access tokens are short-lived (<=15 min).
            _policy.Report(DegradedSites.TokenBlacklist, ex);
            return _policy.For(DegradedSites.TokenBlacklist) == DegradedModePolicy.FailClosed;
        }
    }

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
