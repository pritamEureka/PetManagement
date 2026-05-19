using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.Marketplace.Dtos;

// ----- Store owner / store -----------------------------------------------------

public record StoreOwnerProfileDto(
    Guid Id, Guid UserId, string LegalName, string? BusinessName,
    string? TradeLicenseNumber, string? NationalIdNumber, string? TaxId,
    string? TradeLicenseDocUrl, string? NationalIdDocUrl, string? AddressProofDocUrl,
    ApprovalStatus KycStatus, string? AdminNotes,
    DateTime? SubmittedAt, DateTime? DecidedAt);

public record SubmitStoreOwnerProfileInput(
    string LegalName, string? BusinessName,
    string? TradeLicenseNumber, string? NationalIdNumber, string? TaxId,
    string? TradeLicenseDocUrl, string? NationalIdDocUrl, string? AddressProofDocUrl,
    IReadOnlyList<StoreDocumentInput>? AdditionalDocuments);

public record StoreDocumentInput(StoreDocumentType Type, string FileName, string Url, string? Notes);

public record StoreDto(
    Guid Id, Guid OwnerUserId, string Name, string? Description,
    string? LogoUrl, string? BannerUrl,
    string? Address, string? City, string? Country,
    string? PhoneNumber, string? Email,
    ApprovalStatus ApprovalStatus, decimal CommissionPercent,
    decimal AvgRating, int ReviewCount, int ProductCount,
    DateTime CreatedAt);

public record RegisterStoreInput(
    string Name, string? Description,
    string? LogoUrl, string? BannerUrl,
    string? Address, string? City, string? Country,
    string? PhoneNumber, string? Email);

public record UpdateStoreInput(
    string Name, string? Description,
    string? LogoUrl, string? BannerUrl,
    string? Address, string? City, string? Country,
    string? PhoneNumber, string? Email);

// ----- Catalog / product ------------------------------------------------------

public record ProductCategoryDto(Guid Id, string Name, string Slug, Guid? ParentCategoryId, int ProductCount);
public record CreateProductCategoryInput(string Name, string Slug, Guid? ParentCategoryId);

public record ProductSummaryDto(
    Guid Id, string Name, string Sku,
    decimal Price, decimal? DiscountPrice,
    int StockQuantity, bool IsActive, bool IsFeatured,
    decimal RatingAverage, int RatingCount,
    Guid StoreId, string StoreName,
    Guid? CategoryId, string? CategoryName,
    Guid? BrandId, string? BrandName,
    IReadOnlyList<string> ImageUrls,
    DateTime CreatedAt);

public record ProductDetailDto(
    Guid Id, string Name, string Sku, string? Description,
    decimal Price, decimal? DiscountPrice,
    int StockQuantity, bool IsActive, bool IsFeatured,
    decimal RatingAverage, int RatingCount,
    Guid StoreId, string StoreName, ApprovalStatus StoreApprovalStatus,
    Guid? CategoryId, string? CategoryName,
    Guid? BrandId, string? BrandName,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<ProductReviewDto> RecentReviews,
    DateTime CreatedAt);

public record CreateProductInput(
    string Name, string Sku, string? Description,
    decimal Price, decimal? DiscountPrice,
    int StockQuantity,
    Guid? CategoryId, Guid? BrandId,
    IReadOnlyList<string>? ImageUrls);

public record UpdateProductInput(
    string Name, string? Description,
    decimal Price, decimal? DiscountPrice,
    Guid? CategoryId, Guid? BrandId,
    bool IsActive, IReadOnlyList<string>? ImageUrls);

public record AdjustInventoryInput(int QuantityChange, InventoryReason Reason, string? Notes);

public record InventoryAdjustmentDto(
    Guid Id, Guid ProductId, int QuantityChange, int QuantityAfter,
    InventoryReason Reason, string? Notes, Guid? PerformedById, DateTime CreatedAt);

// ----- Cart -------------------------------------------------------------------

public record CartItemDto(
    Guid Id, Guid ProductId, string ProductName, string? ImageUrl,
    Guid StoreId, string StoreName,
    int Quantity, decimal UnitPrice, decimal Total, int StockAvailable);

public record CartDto(
    Guid Id, string Currency,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal, int TotalItems);

public record AddToCartInput(Guid ProductId, int Quantity);
public record UpdateCartItemInput(int Quantity);

// ----- Order ------------------------------------------------------------------

public record CheckoutInput(
    Guid? ShippingAddressId,
    string? ShippingAddress, string? ShippingCity, string? ShippingCountry,
    string? PaymentMethod);

public record OrderItemDto(
    Guid Id, Guid ProductId, string ProductName, string? ImageUrl,
    Guid StoreId, string StoreName,
    int Quantity, decimal UnitPrice, decimal Total, decimal CommissionAmount);

public record OrderDto(
    Guid Id, string OrderNumber, Guid UserId,
    decimal Subtotal, decimal ShippingFee, decimal Tax, decimal Total,
    OrderStatus Status, PaymentStatus PaymentStatus, ShipmentStatus ShipmentStatus,
    string ShippingAddress, string? ShippingCity, string? ShippingCountry,
    string? TrackingNumber,
    IReadOnlyList<OrderItemDto> Items,
    DateTime CreatedAt,
    string? PaymentCheckoutUrl = null);

public record UpdateOrderStatusInput(OrderStatus Status);
public record UpdateShipmentStatusInput(ShipmentStatus Status, string? TrackingNumber);

// ----- Reviews ----------------------------------------------------------------

public record ProductReviewDto(
    Guid Id, Guid ProductId, Guid UserId, string UserDisplayName, string? UserAvatarUrl,
    int Rating, string? Comment, DateTime CreatedAt);

public record CreateProductReviewInput(int Rating, string? Comment);

public record StoreReviewDto(
    Guid Id, Guid StoreId, Guid UserId, string UserDisplayName, string? UserAvatarUrl,
    int Rating, string? Comment, bool IsVerifiedPurchase, DateTime CreatedAt);

public record CreateStoreReviewInput(int Rating, string? Comment, Guid? OrderId);

// ----- Shipping address book --------------------------------------------------

public record ShippingAddressDto(
    Guid Id, string Label, string RecipientName, string PhoneNumber,
    string AddressLine1, string? AddressLine2,
    string? City, string? State, string? Country, string? PostalCode,
    bool IsDefault);

public record UpsertShippingAddressInput(
    string Label, string RecipientName, string PhoneNumber,
    string AddressLine1, string? AddressLine2,
    string? City, string? State, string? Country, string? PostalCode,
    bool IsDefault);

// ----- Commission -------------------------------------------------------------

public record CommissionConfigurationDto(
    Guid Id, CommissionScope Scope,
    Guid? StoreId, string? StoreName,
    Guid? CategoryId, string? CategoryName,
    decimal CommissionPercent, decimal? FlatFee,
    decimal? MinCommission, decimal? MaxCommission,
    DateTime EffectiveFrom, DateTime? EffectiveTo,
    string? Notes);

public record UpsertCommissionConfigurationInput(
    CommissionScope Scope, Guid? StoreId, Guid? CategoryId,
    decimal CommissionPercent, decimal? FlatFee,
    decimal? MinCommission, decimal? MaxCommission,
    DateTime EffectiveFrom, DateTime? EffectiveTo, string? Notes);

// ----- Returns / refunds ------------------------------------------------------

public record CreateReturnRequestInput(Guid OrderItemId, string Reason);
public record ReturnRequestDto(
    Guid Id, Guid OrderItemId, Guid UserId,
    string Reason, ApprovalStatus Status, decimal? RefundAmount,
    DateTime CreatedAt);

// ----- Sales report -----------------------------------------------------------

public record StoreSalesReportDto(
    Guid StoreId, DateTime From, DateTime To,
    decimal GrossRevenue, decimal Commission, decimal NetRevenue,
    int OrdersCount, int ItemsSold,
    IReadOnlyList<DailySalesPoint> Daily,
    IReadOnlyList<TopProductDto> TopProducts);

public record DailySalesPoint(DateOnly Date, decimal Revenue, int Orders);
public record TopProductDto(Guid ProductId, string Name, int UnitsSold, decimal Revenue);

// ----- Generic paging ---------------------------------------------------------

public record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)Total / PageSize);
}
