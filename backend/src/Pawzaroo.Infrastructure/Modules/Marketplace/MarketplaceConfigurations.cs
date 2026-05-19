using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.ToTable("stores");
        b.HasIndex(s => s.OwnerUserId).IsUnique();
        b.HasIndex(s => s.ApprovalStatus);
        b.HasIndex(s => s.Name);
        b.Property(s => s.Name).HasMaxLength(128).IsRequired();
        b.Property(s => s.Description).HasMaxLength(2000);
        b.Property(s => s.CommissionPercent).HasPrecision(5, 2);

        b.HasOne(s => s.OwnerUser).WithMany().HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StoreOwnerProfileConfiguration : IEntityTypeConfiguration<StoreOwnerProfile>
{
    public void Configure(EntityTypeBuilder<StoreOwnerProfile> b)
    {
        b.ToTable("store_owner_profiles");
        b.HasIndex(p => p.UserId).IsUnique();
        b.HasIndex(p => p.KycStatus);
        b.Property(p => p.LegalName).HasMaxLength(256).IsRequired();
        b.Property(p => p.BusinessName).HasMaxLength(256);
        b.Property(p => p.TradeLicenseNumber).HasMaxLength(128);
        b.Property(p => p.NationalIdNumber).HasMaxLength(64);
        b.Property(p => p.TaxId).HasMaxLength(64);

        b.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StoreDocumentConfiguration : IEntityTypeConfiguration<StoreDocument>
{
    public void Configure(EntityTypeBuilder<StoreDocument> b)
    {
        b.ToTable("store_documents");
        b.HasIndex(d => d.StoreOwnerProfileId);
        b.Property(d => d.FileName).HasMaxLength(256).IsRequired();
        b.Property(d => d.Url).HasMaxLength(1024).IsRequired();

        b.HasOne(d => d.StoreOwnerProfile).WithMany()
            .HasForeignKey(d => d.StoreOwnerProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("product_categories");
        b.HasIndex(c => c.Slug).IsUnique();
        b.Property(c => c.Name).HasMaxLength(128).IsRequired();
        b.Property(c => c.Slug).HasMaxLength(128).IsRequired();
        b.HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> b)
    {
        b.ToTable("brands");
        b.HasIndex(x => x.Name);
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasIndex(p => new { p.StoreId, p.Sku }).IsUnique();
        b.HasIndex(p => p.IsActive);
        b.HasIndex(p => p.IsFeatured);
        b.HasIndex(p => p.CategoryId);
        b.HasIndex(p => p.BrandId);
        b.HasIndex(p => p.CreatedAt);
        b.HasIndex(p => p.Name);

        b.Property(p => p.Name).HasMaxLength(256).IsRequired();
        b.Property(p => p.Sku).HasMaxLength(64).IsRequired();
        b.Property(p => p.Description).HasMaxLength(8000);
        b.Property(p => p.Price).HasPrecision(18, 2);
        b.Property(p => p.DiscountPrice).HasPrecision(18, 2);
        b.Property(p => p.RatingAverage).HasPrecision(4, 2);

        b.HasOne(p => p.Store).WithMany(s => s.Products).HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(p => p.Brand).WithMany(br => br.Products).HasForeignKey(p => p.BrandId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> b)
    {
        b.ToTable("product_images");
        b.HasIndex(i => i.ProductId);
        b.Property(i => i.Url).HasMaxLength(1024).IsRequired();
        b.HasOne(i => i.Product).WithMany(p => p.Images).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> b)
    {
        b.ToTable("product_reviews");
        b.HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
        b.HasOne(r => r.Product).WithMany(p => p.Reviews).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductReviewImageConfiguration : IEntityTypeConfiguration<ProductReviewImage>
{
    public void Configure(EntityTypeBuilder<ProductReviewImage> b)
    {
        b.ToTable("product_review_images");
        b.HasIndex(i => i.ProductReviewId);
        b.Property(i => i.Url).HasMaxLength(1024).IsRequired();
        b.HasOne(i => i.ProductReview).WithMany(r => r.Images).HasForeignKey(i => i.ProductReviewId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.ToTable("wishlist_items");
        b.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
        b.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.ToTable("coupons");
        b.HasIndex(c => c.Code).IsUnique();
        b.Property(c => c.Code).HasMaxLength(64).IsRequired();
        b.Property(c => c.Value).HasPrecision(18, 2);
        b.Property(c => c.MinOrderAmount).HasPrecision(18, 2);
    }
}

public class StoreReviewConfiguration : IEntityTypeConfiguration<StoreReview>
{
    public void Configure(EntityTypeBuilder<StoreReview> b)
    {
        b.ToTable("store_reviews");
        b.HasIndex(r => new { r.StoreId, r.UserId, r.OrderId }).IsUnique();
        b.Property(r => r.Comment).HasMaxLength(2000);
        b.HasOne(r => r.Store).WithMany().HasForeignKey(r => r.StoreId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("carts");
        b.HasIndex(c => new { c.UserId, c.Status });
        b.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("cart_items");
        b.HasIndex(i => new { i.UserId, i.ProductId }).IsUnique();
        b.HasIndex(i => i.CartId);
        b.Property(i => i.UnitPriceSnapshot).HasPrecision(18, 2);
        b.HasOne(i => i.Cart).WithMany(c => c.Items).HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.User).WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> b)
    {
        b.ToTable("inventory_adjustments");
        b.HasIndex(i => new { i.ProductId, i.CreatedAt });
        b.HasIndex(i => i.OrderId);
        b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Order).WithMany().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(i => i.PerformedBy).WithMany().HasForeignKey(i => i.PerformedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasIndex(o => o.OrderNumber).IsUnique();
        b.HasIndex(o => new { o.UserId, o.CreatedAt });
        b.HasIndex(o => o.Status);
        b.HasIndex(o => o.PaymentStatus);
        b.HasIndex(o => o.ShipmentStatus);
        b.Property(o => o.Subtotal).HasPrecision(18, 2);
        b.Property(o => o.ShippingFee).HasPrecision(18, 2);
        b.Property(o => o.Tax).HasPrecision(18, 2);
        b.Property(o => o.DiscountAmount).HasPrecision(18, 2);
        b.Property(o => o.CouponCode).HasMaxLength(64);
        b.Property(o => o.Total).HasPrecision(18, 2);
        b.Property(o => o.OrderNumber).HasMaxLength(32).IsRequired();
        b.Property(o => o.ShippingAddress).HasMaxLength(512).IsRequired();
        b.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items");
        b.HasIndex(i => new { i.StoreId, i.OrderId });
        b.Property(i => i.UnitPrice).HasPrecision(18, 2);
        b.Property(i => i.Total).HasPrecision(18, 2);
        b.Property(i => i.CommissionAmount).HasPrecision(18, 2);
        b.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(i => i.Store).WithMany().HasForeignKey(i => i.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments");
        b.HasIndex(p => p.OrderId);
        b.HasIndex(p => p.TransactionRef);
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.Property(p => p.Method).HasMaxLength(32).IsRequired();
        b.Property(p => p.TransactionRef).HasMaxLength(128);
        b.HasOne(p => p.Order).WithMany(o => o.Payments).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> b)
    {
        b.ToTable("return_requests");
        b.HasIndex(r => r.OrderItemId);
        b.HasIndex(r => r.Status);
        b.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        b.Property(r => r.RefundAmount).HasPrecision(18, 2);
        b.HasOne(r => r.OrderItem).WithMany().HasForeignKey(r => r.OrderItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CommissionConfigurationEf : IEntityTypeConfiguration<CommissionConfiguration>
{
    public void Configure(EntityTypeBuilder<CommissionConfiguration> b)
    {
        b.ToTable("commission_configurations");
        b.HasIndex(c => new { c.Scope, c.StoreId, c.CategoryId, c.EffectiveFrom });
        b.Property(c => c.CommissionPercent).HasPrecision(5, 2);
        b.Property(c => c.FlatFee).HasPrecision(18, 2);
        b.Property(c => c.MinCommission).HasPrecision(18, 2);
        b.Property(c => c.MaxCommission).HasPrecision(18, 2);
        b.HasOne(c => c.Store).WithMany().HasForeignKey(c => c.StoreId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Category).WithMany().HasForeignKey(c => c.CategoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShippingAddressConfiguration : IEntityTypeConfiguration<ShippingAddress>
{
    public void Configure(EntityTypeBuilder<ShippingAddress> b)
    {
        b.ToTable("shipping_addresses");
        b.HasIndex(a => new { a.UserId, a.IsDefault });
        b.Property(a => a.Label).HasMaxLength(32).IsRequired();
        b.Property(a => a.RecipientName).HasMaxLength(128).IsRequired();
        b.Property(a => a.PhoneNumber).HasMaxLength(32).IsRequired();
        b.Property(a => a.AddressLine1).HasMaxLength(256).IsRequired();
        b.Property(a => a.AddressLine2).HasMaxLength(256);
        b.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
