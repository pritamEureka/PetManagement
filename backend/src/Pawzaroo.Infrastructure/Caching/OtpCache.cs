using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// OTP code cache. Keyed by the *subject* (email/phone). Separate attempts
/// counter so we can lock out after N failed verifies without nuking the code.
/// </summary>
public class OtpCache : IOtpCache
{
    private readonly CacheHelper _cache;
    private readonly IDegradedModePolicy _policy;

    public OtpCache(CacheHelper cache, IDegradedModePolicy? policy = null)
    {
        _cache = cache;
        _policy = policy ?? new DegradedModePolicyService();
    }

    public async Task SetAsync(string subject, string code, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            await _cache.Db.StringSetAsync(RedisKeys.Otp(subject), code, ttl);
            await _cache.Db.KeyDeleteAsync(RedisKeys.OtpAttempts(subject));
        }
        catch (RedisException) { /* OTP issuance must surface failure to caller */ throw; }
    }

    public async Task<string?> GetAsync(string subject, CancellationToken ct = default)
    {
        try
        {
            var v = await _cache.Db.StringGetAsync(RedisKeys.Otp(subject));
            return v.IsNullOrEmpty ? null : (string?)v;
        }
        catch (RedisException) { return null; }
    }

    public async Task<int> RecordAttemptAsync(string subject, CancellationToken ct = default)
    {
        var key = RedisKeys.OtpAttempts(subject);
        try
        {
            var n = await _cache.Db.StringIncrementAsync(key, 1);
            if (n == 1) await _cache.Db.KeyExpireAsync(key, RedisTtls.OtpAttempts);
            return (int)n;
        }
        catch (RedisException ex)
        {
            // Brute-force counters must never silently allow: default FailClosed reports "too many attempts".
            _policy.Report(DegradedSites.OtpCounters, ex);
            if (_policy.For(DegradedSites.OtpCounters) == DegradedModePolicy.FailClosed) return int.MaxValue;
            return 0;
        }
    }

    public Task ClearAsync(string subject, CancellationToken ct = default)
    {
        var t1 = _cache.RemoveAsync(RedisKeys.Otp(subject));
        var t2 = _cache.RemoveAsync(RedisKeys.OtpAttempts(subject));
        return Task.WhenAll(t1, t2);
    }
}
