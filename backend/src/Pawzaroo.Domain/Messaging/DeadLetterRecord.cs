using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Messaging;

public enum DeadLetterStatus { Pending = 0, Redriven = 1, Discarded = 2 }

/// <summary>
/// Durable copy of a message parked on a <c>{topic}.dlq</c>, so operators can inspect,
/// re-drive (republish to the original topic) or discard it from the admin UI.
/// </summary>
public class DeadLetterRecord : BaseEntity
{
    /// <summary>Original (source) topic the message was consumed from.</summary>
    public string Topic { get; set; } = default!;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public string? Key { get; set; }
    public string Payload { get; set; } = default!;

    /// <summary>JSON object of header name to UTF-8 value, as received.</summary>
    public string? HeadersJson { get; set; }

    public string ConsumerGroup { get; set; } = "";
    public string Error { get; set; } = default!;

    /// <summary>"Transient" (retries exhausted) or "Permanent" (validation / deserialization / not-found).</summary>
    public string FailureKind { get; set; } = "Transient";

    public int Attempts { get; set; } = 1;
    public DateTime FirstFailedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastFailedAt { get; set; }

    public DeadLetterStatus Status { get; set; } = DeadLetterStatus.Pending;
    public int RedriveCount { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
}
