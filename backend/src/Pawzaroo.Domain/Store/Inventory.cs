using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

public enum InventoryReason { Purchase = 0, Sale = 1, Return = 2, Adjustment = 3, Damage = 4, Restock = 5 }

/// <summary>
/// Append-only stock movement log. Product.StockQuantity is the materialized
/// running total; this table is the audit trail for *why* it changed.
/// </summary>
public class InventoryAdjustment : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public int QuantityChange { get; set; }      // negative = stock out
    public int QuantityAfter { get; set; }       // snapshot
    public InventoryReason Reason { get; set; }
    public string? Notes { get; set; }
    public Guid? PerformedById { get; set; }
    public User? PerformedBy { get; set; }
}
