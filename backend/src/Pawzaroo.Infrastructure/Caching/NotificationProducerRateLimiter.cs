using System.Security.Cryptography;
using System.Text;
using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Caps notification production at <c>NotifyProducerLimit</c> per
/// <see cref="RedisTtls.NotifyProducerRl"/> window per (recipient, title hash).
///
/// Why hash the title: we want "Alice liked your post" floods to throttle but
/// "Bob commented on your post" to pass independently — without storing the
/// whole title as a Redis key.
/// </summary>
public class NotificationProducerRateLimiter : INotificationProducerRateLimiter
{
    private const int NotifyProducerLimit = 5;

    private readonly CacheHelper _cache;
    public NotificationProducerRateLimiter(CacheHelper cache) => _cache = cache;

    public async Task<bool> ShouldThrottleAsync(Guid recipientId, string title, CancellationToken ct = default)
    {
        var key = RedisKeys.NotifyProducerRl(recipientId, HashTitle(title));
        try
        {
            var count = await _cache.Db.StringIncrementAsync(key);
            if (count == 1)
                await _cache.Db.KeyExpireAsync(key, RedisTtls.NotifyProducerRl, ExpireWhen.HasNoExpiry);
            return count > NotifyProducerLimit;
        }
        catch (RedisException)
        {
            // Fail-open: if Redis is down don't drop notifications.
            return false;
        }
    }

    private static string HashTitle(string title)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!SHA1.TryHashData(Encoding.UTF8.GetBytes(title), bytes, out _))
            return "x";
        return Convert.ToHexString(bytes[..6]);
    }
}
