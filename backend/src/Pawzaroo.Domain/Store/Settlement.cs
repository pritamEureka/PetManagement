using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

public enum LedgerEntryType { Earning = 1, Commission = 2, Refund = 3, Reversal = 4, Payout = 5 }

public enum SettlementBatchStatus { Generated = 0, Paid = 1, Reconciled = 2 }

public enum StorePayoutStatus { Pending = 0, Paid = 1, Reconciled = 2, Failed = 3 }

/// <summary>
/// Immutable, append-only vendor ledger line. Amount is signed (+ credit to the store,
/// - debit). Never updated or deleted; corrections are new entries.
/// </summary>
public class LedgerEntry : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public LedgerEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    /// <summary>Entries become payable once this hold date has passed.</summary>
    public DateTime AvailableAt { get; set; }
    public string? Memo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SettlementBatch : BaseEntity
{
    public string Reference { get; set; } = default!;
    public SettlementBatchStatus Status { get; set; } = SettlementBatchStatus.Generated;
    public string Currency { get; set; } = "BDT";
    public decimal TotalAmount { get; set; }
    public int PayoutCount { get; set; }
    public DateTime CutoffAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ReconciledAt { get; set; }

    public ICollection<StorePayout> Payouts { get; set; } = new List<StorePayout>();
}

public class StorePayout : BaseEntity
{
    public Guid SettlementBatchId { get; set; }
    public SettlementBatch SettlementBatch { get; set; } = default!;
    public Guid StoreId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    public StorePayoutStatus Status { get; set; } = StorePayoutStatus.Pending;
    public string? GatewayReference { get; set; }
    public decimal? GatewayAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? ReconciledAt { get; set; }
}

/// <summary>Join row recording which ledger entries a payout settled (keeps LedgerEntry immutable).</summary>
public class StorePayoutLine : BaseEntity
{
    public Guid StorePayoutId { get; set; }
    public Guid LedgerEntryId { get; set; }
}
