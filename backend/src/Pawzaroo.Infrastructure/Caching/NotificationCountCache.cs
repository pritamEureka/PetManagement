using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Notification unread counter. Producers (notification service) BUMP;
/// API clients reset to 0 on "mark all read" and decrement on per-row read.
/// The DB remains the source of truth — this cache exists so the bell icon
/// doesn't issue COUNT(*) per page.
/// </summary>
public class NotificationCountCache : INotificationCountCache
{
    private readonly CacheHelper _cache;
    public NotificationCountCache(CacheHelper cache) => _cache = cache;

    public async Task<long> GetUnreadAsync(Guid userId, CancellationToken ct = default)
        => await _cache.GetCounterAsync(RedisKeys.NotifyUnread(userId)) ?? 0L;

    public Task<long> BumpUnreadAsync(Guid userId, int delta = 1, CancellationToken ct = default)
        => _cache.IncrementAsync(RedisKeys.NotifyUnread(userId), delta, RedisTtls.NotifyUnread);

    public async Task DecrementUnreadAsync(Guid userId, int delta = 1, CancellationToken ct = default)
    {
        // Clamp at 0 — race between decrement and reset shouldn't produce negative bell counts.
        var key = RedisKeys.NotifyUnread(userId);
        try
        {
            var v = await _cache.Db.StringDecrementAsync(key, delta);
            if (v < 0) await _cache.Db.StringSetAsync(key, 0, RedisTtls.NotifyUnread);
        }
        catch (RedisException) { /* swallow — DB is source of truth */ }
    }

    public Task ResetUnreadAsync(Guid userId, CancellationToken ct = default)
        => _cache.SetAsync(RedisKeys.NotifyUnread(userId), 0L, RedisTtls.NotifyUnread);

    public async Task<DateTime?> GetLastFetchAsync(Guid userId, CancellationToken ct = default)
        => await _cache.GetAsync<DateTime?>(RedisKeys.NotifyLast(userId));

    public Task SetLastFetchAsync(Guid userId, DateTime at, CancellationToken ct = default)
        => _cache.SetAsync(RedisKeys.NotifyLast(userId), at, RedisTtls.NotifyUnread);
}
