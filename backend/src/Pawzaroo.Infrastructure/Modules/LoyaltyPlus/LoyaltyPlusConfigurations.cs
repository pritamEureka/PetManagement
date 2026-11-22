using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Modules.LoyaltyPlus;

public class ProductBundleConfiguration : IEntityTypeConfiguration<ProductBundle>
{
    public void Configure(EntityTypeBuilder<ProductBundle> b)
    {
        b.ToTable("product_bundles");
        b.HasIndex(x => x.StoreId);
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.HasMany(x => x.Items).WithOne(i => i.Bundle).HasForeignKey(i => i.BundleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductBundleItemConfiguration : IEntityTypeConfiguration<ProductBundleItem>
{
    public void Configure(EntityTypeBuilder<ProductBundleItem> b)
    {
        b.ToTable("product_bundle_items");
        b.HasIndex(x => x.BundleId);
        b.HasIndex(x => x.ProductId);
    }
}

public class BundleCartDiscountConfiguration : IEntityTypeConfiguration<BundleCartDiscount>
{
    public void Configure(EntityTypeBuilder<BundleCartDiscount> b)
    {
        b.ToTable("bundle_cart_discounts");
        b.HasIndex(x => new { x.UserId, x.BundleId }).IsUnique();
    }
}

public class ProductStockThresholdConfiguration : IEntityTypeConfiguration<ProductStockThreshold>
{
    public void Configure(EntityTypeBuilder<ProductStockThreshold> b)
    {
        b.ToTable("product_stock_thresholds");
        b.HasIndex(x => new { x.ProductId, x.VariantId });
    }
}

public class ReplenishmentReminderConfiguration : IEntityTypeConfiguration<ReplenishmentReminder>
{
    public void Configure(EntityTypeBuilder<ReplenishmentReminder> b)
    {
        b.ToTable("replenishment_reminders");
        b.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        b.HasIndex(x => x.ExpectedRunOutAt);
    }
}

public class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> b)
    {
        b.ToTable("loyalty_accounts");
        b.HasIndex(x => x.UserId).IsUnique();
    }
}

public class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> b)
    {
        b.ToTable("loyalty_transactions");
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        // Idempotency: at most one Earn / Redeem / Reverse row per order.
        b.HasIndex(x => new { x.OrderId, x.Type }).IsUnique().HasFilter("order_id IS NOT NULL");
        b.Property(x => x.Note).HasMaxLength(300);
    }
}

public class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> b)
    {
        b.ToTable("referral_codes");
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(24).IsRequired();
    }
}

public class ReferralRedemptionConfiguration : IEntityTypeConfiguration<ReferralRedemption>
{
    public void Configure(EntityTypeBuilder<ReferralRedemption> b)
    {
        b.ToTable("referral_redemptions");
        b.HasIndex(x => x.ReferredUserId).IsUnique();
        b.HasIndex(x => x.ReferrerUserId);
        b.Property(x => x.Code).HasMaxLength(24).IsRequired();
    }
}

public class GiftCardConfiguration : IEntityTypeConfiguration<GiftCard>
{
    public void Configure(EntityTypeBuilder<GiftCard> b)
    {
        b.ToTable("gift_cards");
        b.HasIndex(x => x.CodeHash).IsUnique();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        b.Property(x => x.InitialAmount).HasPrecision(18, 2);
        b.Property(x => x.Balance).HasPrecision(18, 2);
        b.Property(x => x.RecipientEmail).HasMaxLength(256);
        b.Property(x => x.Message).HasMaxLength(500);
    }
}

public class GiftCardTransactionConfiguration : IEntityTypeConfiguration<GiftCardTransaction>
{
    public void Configure(EntityTypeBuilder<GiftCardTransaction> b)
    {
        b.ToTable("gift_card_transactions");
        b.HasIndex(x => x.GiftCardId);
        b.HasIndex(x => new { x.OrderId, x.GiftCardId, x.Type }).IsUnique().HasFilter("order_id IS NOT NULL");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(300);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("promotions");
        b.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt });
        b.Property(x => x.Name).HasMaxLength(160).IsRequired();
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.Property(x => x.MinSubtotal).HasPrecision(18, 2);
    }
}

public class OrderAppliedAdjustmentConfiguration : IEntityTypeConfiguration<OrderAppliedAdjustment>
{
    public void Configure(EntityTypeBuilder<OrderAppliedAdjustment> b)
    {
        b.ToTable("order_applied_adjustments");
        b.HasIndex(x => x.OrderId);
        b.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(300);
    }
}

public class SponsoredPlacementConfiguration : IEntityTypeConfiguration<SponsoredPlacement>
{
    public void Configure(EntityTypeBuilder<SponsoredPlacement> b)
    {
        b.ToTable("sponsored_placements");
        b.HasIndex(x => new { x.Status, x.IsActive, x.StartsAt, x.EndsAt });
        b.HasIndex(x => x.StoreId);
        b.HasIndex(x => x.ProductId);
        b.Property(x => x.Budget).HasPrecision(18, 2);
        b.Property(x => x.Spent).HasPrecision(18, 2);
        b.Property(x => x.BidWeight).HasPrecision(8, 2);
    }
}
