using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Infrastructure.Caching;

namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// SETNX-based idempotency claim. SETNX returns true exactly once for a given
/// (group, eventId); duplicates within the TTL fail to claim and the consumer
/// commits without re-processing.
/// </summary>
public class RedisConsumerIdempotency : IConsumerIdempotency
{
    private readonly CacheHelper _cache;
    public RedisConsumerIdempotency(CacheHelper cache) => _cache = cache;

    public async Task<bool> TryClaimAsync(string consumerGroup, Guid eventId, CancellationToken ct = default)
        => await _cache.SetIfNotExistsAsync(RedisKeys.Inboxed(consumerGroup, eventId), DateTime.UtcNow, RedisTtls.Inboxed);

    public Task ReleaseAsync(string consumerGroup, Guid eventId, CancellationToken ct = default)
        => _cache.RemoveAsync(RedisKeys.Inboxed(consumerGroup, eventId));
}
