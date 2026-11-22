using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.ToTable("product_variants");
        b.HasIndex(v => new { v.ProductId, v.Sku }).IsUnique();
        b.Property(v => v.Sku).HasMaxLength(64).IsRequired();
        b.Property(v => v.AttributesJson).HasMaxLength(2000).IsRequired();
        b.Property(v => v.PriceOverride).HasPrecision(18, 2);
        b.HasOne(v => v.Product).WithMany().HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemVariantConfiguration : IEntityTypeConfiguration<CartItemVariant>
{
    public void Configure(EntityTypeBuilder<CartItemVariant> b)
    {
        b.ToTable("cart_item_variants");
        b.HasIndex(x => x.CartItemId).IsUnique();
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> b)
    {
        b.ToTable("stock_reservations");
        b.HasIndex(r => new { r.Status, r.ExpiresAt });
        b.HasIndex(r => r.UserId);
        b.HasIndex(r => new { r.ProductId, r.VariantId });
    }
}

public class ProductSubscriptionConfiguration : IEntityTypeConfiguration<ProductSubscription>
{
    public void Configure(EntityTypeBuilder<ProductSubscription> b)
    {
        b.ToTable("product_subscriptions");
        b.HasIndex(s => new { s.Status, s.NextRunAt });
        b.HasIndex(s => s.UserId);
        b.Property(s => s.DiscountPercent).HasPrecision(5, 2);
        b.Property(s => s.LastError).HasMaxLength(500);
        b.Property(s => s.PaymentMethod).HasMaxLength(32).IsRequired();
        b.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> b)
    {
        b.ToTable("saved_searches");
        b.HasIndex(s => s.UserId);
        b.HasIndex(s => s.AlertEnabled);
        b.Property(s => s.Name).HasMaxLength(100).IsRequired();
        b.Property(s => s.QueryJson).HasMaxLength(4000).IsRequired();
    }
}

public class RecentlyViewedItemConfiguration : IEntityTypeConfiguration<RecentlyViewedItem>
{
    public void Configure(EntityTypeBuilder<RecentlyViewedItem> b)
    {
        b.ToTable("recently_viewed_items");
        b.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.ViewedAt });
    }
}

public class PriceWatchConfiguration : IEntityTypeConfiguration<PriceWatch>
{
    public void Configure(EntityTypeBuilder<PriceWatch> b)
    {
        b.ToTable("price_watches");
        b.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        b.HasIndex(w => w.IsActive);
        b.Property(w => w.BaselinePrice).HasPrecision(18, 2);
        b.Property(w => w.TargetPrice).HasPrecision(18, 2);
        b.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StoreLocationConfiguration : IEntityTypeConfiguration<StoreLocation>
{
    public void Configure(EntityTypeBuilder<StoreLocation> b)
    {
        b.ToTable("store_locations");
        b.HasIndex(x => x.StoreId).IsUnique();
    }
}
