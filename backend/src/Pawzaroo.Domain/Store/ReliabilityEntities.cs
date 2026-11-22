using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

public enum IdempotencyStatus { Started = 0, Completed = 1 }

/// <summary>
/// Durable HTTP idempotency-key record. Unique per (UserId, Key, Method, Path).
/// UserId is Guid.Empty for anonymous callers.
/// </summary>
public class IdempotencyRecord : BaseEntity
{
    public string Key { get; set; } = default!;
    public Guid UserId { get; set; }
    public string Method { get; set; } = default!;
    public string Path { get; set; } = default!;
    /// <summary>SHA-256 (hex) of the request body + query string.</summary>
    public string RequestHash { get; set; } = default!;
    public IdempotencyStatus Status { get; set; } = IdempotencyStatus.Started;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

public enum PaymentAuditEventType
{
    Initiated = 0,
    CallbackReceived = 1,
    Validated = 2,
    Succeeded = 3,
    Failed = 4,
    Cancelled = 5,
    Refunded = 6,
    AmountMismatch = 7,
    DuplicateCallback = 8,
    Reconciled = 9
}

/// <summary>Append-only payment audit row. Updates/deletes are rejected (EF config + save interceptor).</summary>
public class PaymentAuditEntry : BaseEntity
{
    public Guid? PaymentId { get; set; }
    public Guid? OrderId { get; set; }
    public string Provider { get; set; } = default!;
    public PaymentAuditEventType EventType { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? RawPayloadHash { get; set; }
    /// <summary>Redacted JSON (card / credential fields stripped).</summary>
    public string? RawPayloadJson { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Insert-first callback de-duplication row. Unique on (Provider, TransactionId, EventType):
/// the second identical gateway callback hits the unique index and replays <see cref="Result"/>.
/// </summary>
public class PaymentCallbackLog : BaseEntity
{
    public string Provider { get; set; } = default!;
    public string TransactionId { get; set; } = default!;
    /// <summary>Success / Fail / Cancel / Ipn.</summary>
    public string EventType { get; set; } = default!;
    public Guid? OrderId { get; set; }
    /// <summary>Succeeded / Failed / Cancelled (gateway-validated outcome).</summary>
    public string Outcome { get; set; } = "Pending";
    /// <summary>Applied / AmountMismatch / Ignored.</summary>
    public string Result { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
