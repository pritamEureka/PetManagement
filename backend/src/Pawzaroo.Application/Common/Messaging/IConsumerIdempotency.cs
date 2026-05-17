namespace Pawzaroo.Application.Common.Messaging;

/// <summary>
/// Exactly-once-ish: Kafka guarantees at-least-once, so consumers need a way
/// to detect re-delivery. Implementation: a Redis SETNX keyed by
/// (consumerGroup, eventId) with a 72h TTL. Cheap, correct under partition
/// rebalancing, and survives consumer restarts.
/// </summary>
public interface IConsumerIdempotency
{
    /// <summary>Returns true if this is a new event for the consumer group (claim succeeded).</summary>
    Task<bool> TryClaimAsync(string consumerGroup, Guid eventId, CancellationToken ct = default);
    Task ReleaseAsync(string consumerGroup, Guid eventId, CancellationToken ct = default);
}
