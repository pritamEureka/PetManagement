using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// Versioned commission rule. The active rule is the latest non-deleted row
/// whose EffectiveFrom &lt;= now and (EffectiveTo IS NULL OR EffectiveTo &gt; now)
/// optionally scoped by category. Per-store overrides win over category rules,
/// which win over the global rule (Scope = Global, CategoryId NULL).
/// </summary>
public class CommissionConfiguration : AuditableEntity
{
    public CommissionScope Scope { get; set; } = CommissionScope.Global;
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }

    public decimal CommissionPercent { get; set; } = 10m;
    public decimal? FlatFee { get; set; }
    public decimal? MinCommission { get; set; }
    public decimal? MaxCommission { get; set; }

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

public enum CommissionScope { Global = 0, Category = 1, Store = 2 }
