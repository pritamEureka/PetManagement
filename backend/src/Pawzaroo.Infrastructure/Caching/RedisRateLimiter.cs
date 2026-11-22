using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Distributed fixed-window rate limiter, Lua-atomic:
///   INCR key; if value == 1 then EXPIRE key window; return count.
/// One round-trip, no race between INCR + EXPIRE.
///
/// Complements ASP.NET's in-proc <c>RateLimiter</c>: this one is used by the
/// API gateway for *global* limits that span every instance — e.g. login
/// throttling per email, OTP send throttling per phone.
/// </summary>
public class RedisRateLimiter : IRedisRateLimiter
{
    private readonly CacheHelper _cache;

    private const string LuaScript = @"
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { current, ttl }
    ";

    private readonly IDegradedModePolicy _policy;

    public RedisRateLimiter(CacheHelper cache, IDegradedModePolicy? policy = null)
    {
        _cache = cache;
        _policy = policy ?? new DegradedModePolicyService();
    }

    public async Task<RateLimitDecision> CheckAsync(string scope, string partition, int permitLimit, TimeSpan window, CancellationToken ct = default)
    {
        var key = RedisKeys.Rl(scope, partition);
        try
        {
            var result = (RedisResult[])(await _cache.Db.ScriptEvaluateAsync(
                LuaScript,
                new RedisKey[] { key },
                new RedisValue[] { (long)window.TotalMilliseconds }))!;

            var count = (long)result[0];
            var ttlMs = (long)result[1];
            var resetIn = TimeSpan.FromMilliseconds(ttlMs < 0 ? window.TotalMilliseconds : ttlMs);
            var remaining = Math.Max(0, permitLimit - count);
            return new RateLimitDecision(count <= permitLimit, remaining, resetIn);
        }
        catch (RedisException ex)
        {
            // Explicit degraded-mode policy: auth-sensitive scopes fail closed; the rest fail open
            // (the in-proc limiter still protects against runaway abuse). See Security:RedisDegraded:RateLimit.
            _policy.Report(DegradedSites.RateLimit, ex);
            return _policy.ForRateLimitScope(scope) == DegradedModePolicy.FailClosed
                ? new RateLimitDecision(false, 0, window)
                : new RateLimitDecision(true, permitLimit, window);
        }
    }
}
