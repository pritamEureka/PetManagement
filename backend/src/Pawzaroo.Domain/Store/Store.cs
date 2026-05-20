using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

public class Store : AuditableEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public decimal CommissionPercent { get; set; } = 10m;
    public string? AdminNotes { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class ProductCategory : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public Guid? ParentCategoryId { get; set; }
    public ProductCategory? ParentCategory { get; set; }
    public ICollection<ProductCategory> SubCategories { get; set; } = new List<ProductCategory>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Brand : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string? LogoUrl { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Sku { get; set; } = default!;
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Url { get; set; } = default!;
    public int OrderIndex { get; set; }
}

public class ProductReview : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public int Rating { get; set; }
    public string? Comment { get; set; }

    public ICollection<ProductReviewImage> Images { get; set; } = new List<ProductReviewImage>();
}

public class ProductReviewImage : BaseEntity
{
    public Guid ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = default!;
    public string Url { get; set; } = default!;
    public int OrderIndex { get; set; }
}

public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = default!;
    public Guid UserId { get; set; }   // denormalized for fast cart-by-user queries
    public User User { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPriceSnapshot { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public class Order : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string OrderNumber { get; set; } = default!;
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Tax { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public ShipmentStatus ShipmentStatus { get; set; } = ShipmentStatus.NotShipped;
    public string ShippingAddress { get; set; } = default!;
    public string? ShippingCity { get; set; }
    public string? ShippingCountry { get; set; }
    public string? TrackingNumber { get; set; }
    public CourierProvider? Courier { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public DeliveryAssignment? DeliveryAssignment { get; set; }
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public decimal CommissionAmount { get; set; }
}

public class Payment : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "card";
    public string? TransactionRef { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}

public class ReturnRequest : AuditableEntity
{
    public Guid OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public decimal? RefundAmount { get; set; }
}
