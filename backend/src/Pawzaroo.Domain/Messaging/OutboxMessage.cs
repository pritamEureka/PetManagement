using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Messaging;

public enum OutboxStatus { Pending = 0, Dispatched = 1, Failed = 2 }

/// <summary>
/// Transactional outbox row. Producers write a single DB transaction that
/// includes both the domain mutation AND the row in <c>outbox_messages</c>.
/// A separate worker scans Pending rows and ships them to Kafka — guarantees
/// at-least-once delivery without two-phase commits.
///
/// Why this matters: without an outbox, "DB committed but Kafka publish failed"
/// silently loses events; with an outbox, we just retry the dispatch.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public string Topic { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string Version { get; set; } = "1";

    /// <summary>Serialized <see cref="Application.Common.Messaging.EventEnvelope{T}"/>.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>Kafka partition key. Defaults to EventId; aggregate-id is a better choice when ordering matters.</summary>
    public string PartitionKey { get; set; } = default!;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAt { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }

    public string? CorrelationId { get; set; }
    public Guid? UserId { get; set; }
}
