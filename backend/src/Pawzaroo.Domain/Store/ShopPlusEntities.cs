using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

// ---- 8.5 Variants / reservations / subscriptions ---------------------------------

/// <summary>
/// Sellable variant of a product (size/color/...). Product.StockQuantity is kept equal to the
/// sum of variant stock by the variant stock operations so the existing checkout stays consistent.
/// </summary>
public class ProductVariant : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Sku { get; set; } = default!;
    /// <summary>JSON object, e.g. {"size":"L","color":"red"}.</summary>
    public string AttributesJson { get; set; } = "{}";
    /// <summary>Null = use the product price.</summary>
    public decimal? PriceOverride { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Links a cart line to the chosen variant (CartItem itself is untouched).</summary>
public class CartItemVariant : BaseEntity
{
    public Guid CartItemId { get; set; }
    public Guid VariantId { get; set; }
}

public enum ReservationStatus { Active = 0, Released = 1, Expired = 2, Consumed = 3 }

public class StockReservation : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
}

public enum SubscriptionStatus { Active = 0, Paused = 1, Cancelled = 2 }

public class ProductSubscription : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public int IntervalDays { get; set; } = 30;
    public decimal DiscountPercent { get; set; }
    public DateTime NextRunAt { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public int FailureCount { get; set; }
    public string? LastError { get; set; }
    public Guid? LastOrderId { get; set; }
    public DateTime? LastRunAt { get; set; }
    public Guid ShippingAddressId { get; set; }
    public string PaymentMethod { get; set; } = "cod";
}

// ---- 8.7 Discovery ----------------------------------------------------------------

public class SavedSearch : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    /// <summary>Serialized FacetSearchRequest.</summary>
    public string QueryJson { get; set; } = "{}";
    public bool AlertEnabled { get; set; }
    /// <summary>Alert job only reports products created after this instant.</summary>
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}

public class RecentlyViewedItem : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}

public class PriceWatch : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    /// <summary>Effective price when the watch was created / last notified.</summary>
    public decimal BaselinePrice { get; set; }
    /// <summary>Optional: only alert when price reaches this value or below.</summary>
    public decimal? TargetPrice { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Optional geo point for a store (Store has no coordinates) used for distance ranking.</summary>
public class StoreLocation : BaseEntity
{
    public Guid StoreId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
