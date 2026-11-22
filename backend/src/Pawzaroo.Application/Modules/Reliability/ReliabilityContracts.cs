using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.Reliability;

// ---------------- Idempotency ----------------

public enum IdempotencyBeginOutcome { Proceed, Replay, InFlight, Mismatch }

public record IdempotencyBeginResult(
    IdempotencyBeginOutcome Outcome, Guid RecordId, int? StatusCode, string? Body, string? ContentType);

public interface IIdempotencyStore
{
    /// <summary>Claims the key (insert-first on the unique index) or reports what happened to the earlier request.</summary>
    Task<IdempotencyBeginResult> BeginAsync(Guid userId, string key, string method, string path,
        string requestHash, CancellationToken ct = default);

    /// <summary>Stores the deterministic response for replay.</summary>
    Task CompleteAsync(Guid recordId, int statusCode, string? body, string? contentType, CancellationToken ct = default);

    /// <summary>Releases a claimed key (5xx / exception) so the client can retry with the same key.</summary>
    Task AbandonAsync(Guid recordId, CancellationToken ct = default);
}

public interface IIdempotencyCleanupService
{
    /// <summary>Deletes expired records (and stale Started rows). Returns the number removed.</summary>
    Task<int> CleanupExpiredAsync(CancellationToken ct = default);
}

// ---------------- Payment audit ----------------

public record PaymentAuditRecord(
    PaymentAuditEventType EventType,
    string Provider,
    Guid? OrderId = null,
    Guid? PaymentId = null,
    decimal? Amount = null,
    string? Currency = null,
    IDictionary<string, string>? RawPayload = null,
    string? IpAddress = null,
    string? Note = null);

public interface IPaymentAuditService
{
    /// <summary>Appends one immutable audit row (saved immediately; joins an ambient transaction if any).</summary>
    Task RecordAsync(PaymentAuditRecord record, CancellationToken ct = default);
}

// ---------------- Callbacks ----------------

/// <param name="Duplicate">True when this exact callback (Provider, TransactionId, EventType) was handled before.</param>
public record PaymentCallbackProcessResult(
    Guid OrderId, PaymentCallbackOutcome Outcome, bool Duplicate, string Result);

public interface IPaymentCallbackProcessor
{
    /// <summary>
    /// Validates the callback with the gateway, de-duplicates it, verifies amount/currency and applies it to the
    /// order. Returns null when the callback could not be validated at all (no order id).
    /// </summary>
    Task<PaymentCallbackProcessResult?> ProcessAsync(IDictionary<string, string> fields, PaymentCallbackKind kind,
        string? ipAddress, CancellationToken ct = default);
}

// ---------------- Reconciliation ----------------

public record GatewayTransactionStatus(
    string TransactionId, bool Found, bool Paid, string? RawStatus,
    decimal? Amount, string? Currency, string? BankTransactionId);

/// <summary>Optional gateway capability: look a transaction up by our tran_id. Defaults to "not supported".</summary>
public interface IPaymentStatusQuery
{
    bool SupportsQuery => false;

    Task<GatewayTransactionStatus?> QueryByTransactionIdAsync(string transactionId, CancellationToken ct = default)
        => Task.FromResult<GatewayTransactionStatus?>(null);
}

public record ReconciliationItem(
    Guid OrderId, Guid? PaymentId, string Kind, string Severity, string Detail,
    decimal? LocalAmount, decimal? GatewayAmount, string? LocalStatus, string? GatewayStatus);

public class ReconciliationReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public bool GatewayQuerySupported { get; set; }
    public int PaymentsChecked { get; set; }
    public int Matched { get; set; }
    public int Discrepancies => Items.Count;
    public List<ReconciliationItem> Items { get; set; } = new();
}

public interface IPaymentReconciliationService
{
    Task<ReconciliationReport> ReconcileAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
