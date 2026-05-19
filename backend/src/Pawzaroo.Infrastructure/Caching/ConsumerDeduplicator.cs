using Pawzaroo.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Redis SET NX dedup gate for Kafka consumers. Atomically claims an event
/// (scope = consumer-group name, key = stable event identifier) so re-deliveries
/// from rebalances or crash-before-commit are processed at most once within the
/// configured window.
/// </summary>
public class ConsumerDeduplicator : IConsumerDeduplicator
{
    private readonly CacheHelper _cache;
    public ConsumerDeduplicator(CacheHelper cache) => _cache = cache;

    public async Task<bool> TryClaimAsync(string scope, string key, TimeSpan window, CancellationToken ct = default)
    {
        try
        {
            return await _cache.Db.StringSetAsync(
                $"dedup:{scope}:{key}", "1", window, When.NotExists);
        }
        catch (RedisException)
        {
            // Fail-open: if Redis is unavailable, fall back to whatever
            // consumer-side idempotency exists (DB unique constraints, etc.).
            return true;
        }
    }
}
