using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

// ---- Marketplace expansion 1.4: bundles, alerts, replenishment, loyalty, gift cards, promotions, sponsored ----

/// <summary>Store-defined bundle of products sold at a fixed price or a percent discount on the component list price.</summary>
public class ProductBundle : AuditableEntity
{
    public Guid StoreId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Fixed bundle price (per bundle). Null when <see cref="DiscountPercent"/> is used.</summary>
    public decimal? Price { get; set; }
    public decimal? DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProductBundleItem> Items { get; set; } = new List<ProductBundleItem>();
}

public class ProductBundleItem : BaseEntity
{
    public Guid BundleId { get; set; }
    public ProductBundle Bundle { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Records that a user added N copies of a bundle to the cart. The component lines are ordinary CartItems; at
/// checkout the BundleAdjuster grants the discount only for as many bundles as the cart still fully contains.
/// </summary>
public class BundleCartDiscount : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BundleId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Per product (or variant) low-stock threshold and de-dup stamp for alerts.</summary>
public class ProductStockThreshold : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int Threshold { get; set; }
    public DateTime? LastAlertedAt { get; set; }
}

public class ReplenishmentReminder : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime ExpectedRunOutAt { get; set; }
    public DateTime? RemindedAt { get; set; }
    public bool Dismissed { get; set; }
}

// ---- Loyalty / referral ---------------------------------------------------------

public enum LoyaltyTier { Bronze = 0, Silver = 1, Gold = 2, Platinum = 3 }
public enum LoyaltyTransactionType { Earn = 0, Redeem = 1, Expire = 2, Adjust = 3, Reverse = 4 }

public class LoyaltyAccount : BaseEntity
{
    public Guid UserId { get; set; }
    public int Balance { get; set; }
    public int LifetimePoints { get; set; }
    public LoyaltyTier Tier { get; set; } = LoyaltyTier.Bronze;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Immutable ledger row. Points are signed (Earn +, Redeem -, Expire -, Reverse +/-, Adjust +/-).</summary>
public class LoyaltyTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public LoyaltyTransactionType Type { get; set; }
    public int Points { get; set; }
    public Guid? OrderId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

public class ReferralCode : BaseEntity
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReferralRedemption : BaseEntity
{
    public string Code { get; set; } = default!;
    public Guid ReferrerUserId { get; set; }
    public Guid ReferredUserId { get; set; }
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RewardedAt { get; set; }
}

// ---- Gift cards -----------------------------------------------------------------

public enum GiftCardStatus { Pending = 0, Active = 1, Depleted = 2, Expired = 3, Cancelled = 4 }
public enum GiftCardTransactionType { Issue = 0, Redeem = 1, Refund = 2, Adjust = 3 }

public class GiftCard : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public decimal InitialAmount { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "BDT";
    public Guid? PurchaserUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public GiftCardStatus Status { get; set; } = GiftCardStatus.Active;
}

public class GiftCardTransaction : BaseEntity
{
    public Guid GiftCardId { get; set; }
    public GiftCardTransactionType Type { get; set; }
    /// <summary>Signed: Issue +, Redeem -, Refund +, Adjust +/-.</summary>
    public decimal Amount { get; set; }
    public Guid? OrderId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- Promotions (auto-applied; coupons are code-based) -----------------------------

public enum PromotionType { PercentOff = 0, FixedOff = 1, FreeShipping = 2, BuyXGetY = 3 }
public enum PromotionScope { Storewide = 0, Category = 1, Store = 2, Product = 3 }

public class Promotion : AuditableEntity
{
    public string Name { get; set; } = default!;
    public PromotionType Type { get; set; }
    public PromotionScope Scope { get; set; }
    /// <summary>Category / store / product id depending on <see cref="Scope"/>; null for Storewide.</summary>
    public Guid? ScopeRefId { get; set; }
    /// <summary>PercentOff: 0-100. FixedOff: amount. Others ignored.</summary>
    public decimal Value { get; set; }
    /// <summary>BuyXGetY: buy this many ...</summary>
    public int BuyQuantity { get; set; }
    /// <summary>... get this many free.</summary>
    public int GetQuantity { get; set; }
    public decimal MinSubtotal { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool Stackable { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>Audit/reversal record of every pricing adjustment applied to an order.</summary>
public class OrderAppliedAdjustment : BaseEntity
{
    public Guid OrderId { get; set; }
    /// <summary>promotion | bundle | loyalty | giftcard | freeShipping</summary>
    public string Kind { get; set; } = default!;
    public Guid? ReferenceId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    /// <summary>Points redeemed (loyalty kind only).</summary>
    public int? Points { get; set; }
    public DateTime? ReversedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---- Sponsored placements -----------------------------------------------------------

public enum SponsoredPlacementStatus { Pending = 0, Approved = 1, Rejected = 2 }

public class SponsoredPlacement : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal? Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal BidWeight { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public SponsoredPlacementStatus Status { get; set; } = SponsoredPlacementStatus.Pending;
    public Guid? RequestedByUserId { get; set; }
    public int Impressions { get; set; }
}
