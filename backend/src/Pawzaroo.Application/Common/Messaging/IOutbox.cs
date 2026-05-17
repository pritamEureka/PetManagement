namespace Pawzaroo.Application.Common.Messaging;

/// <summary>
/// Transactional outbox writer. Call inside the same SaveChanges as the
/// domain mutation — the row commits with the entity, then the
/// <c>OutboxDispatcherJob</c> ships it to Kafka. Replaces direct
/// <c>IKafkaProducer.PublishAsync</c> calls for events that must not be lost.
/// </summary>
public interface IOutbox
{
    /// <summary>Enqueue an event for asynchronous, durable dispatch.</summary>
    Task EnqueueAsync<T>(string topic, T payload, string? partitionKey = null, CancellationToken ct = default)
        where T : IIntegrationEvent;
}
