using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.ShopPlus;

// ===== 8.5 Variants ==============================================================

public record VariantDto(
    Guid Id, Guid ProductId, string Sku,
    IReadOnlyDictionary<string, string> Attributes,
    decimal? PriceOverride, decimal EffectivePrice,
    int StockQuantity, int Reserved, int Available, bool IsActive);

public record UpsertVariantInput(
    string Sku,
    Dictionary<string, string>? Attributes,
    decimal? PriceOverride,
    int StockQuantity = 0,
    bool IsActive = true);

public record AdjustVariantStockInput(int QuantityChange, InventoryReason Reason = InventoryReason.Adjustment, string? Notes = null);

public record AddVariantToCartInput(Guid VariantId, int Quantity);

// ===== 8.5 Reservations ==========================================================

public record ReservationDto(Guid Id, Guid ProductId, Guid? VariantId, int Quantity, DateTime ExpiresAt, ReservationStatus Status);

public record CheckoutReservationDto(IReadOnlyList<ReservationDto> Reservations, DateTime? ExpiresAt);

// ===== 8.5 Subscriptions / reorder ===============================================

public record SubscriptionDto(
    Guid Id, Guid ProductId, string ProductName, string? ImageUrl, Guid? VariantId, string? VariantSku,
    int Quantity, int IntervalDays, decimal DiscountPercent,
    decimal UnitPrice, decimal DiscountedUnitPrice,
    DateTime NextRunAt, SubscriptionStatus Status, int FailureCount, string? LastError,
    Guid? LastOrderId, DateTime? LastRunAt, Guid ShippingAddressId, string PaymentMethod, DateTime CreatedAt);

public record CreateSubscriptionInput(
    Guid ProductId, Guid? VariantId, int Quantity, int IntervalDays,
    Guid ShippingAddressId, string? PaymentMethod);

public record UpdateSubscriptionInput(int? Quantity, int? IntervalDays, Guid? ShippingAddressId, string? PaymentMethod);

public record ReorderSkippedDto(Guid ProductId, string ProductName, string Reason);

public record ReorderResultDto(int AddedCount, IReadOnlyList<ReorderSkippedDto> Skipped, CartDto Cart);

public record SubscriptionRunSummary(int Processed, int Succeeded, int Failed, int Paused);

// ===== 8.7 Discovery =============================================================

public record FacetSearchRequest(
    string? Q = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? StoreId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    bool? InStockOnly = null,
    /// <summary>"relevance" (default) | "newest" | "price_asc" | "price_desc" | "rating_desc"</summary>
    string? Sort = null,
    double? Lat = null,
    double? Lng = null,
    int Page = 1,
    int PageSize = 24);

public record FacetBucket(string Key, string Label, int Count, bool Selected = false);

public record FacetsDto(
    IReadOnlyList<FacetBucket> Categories,
    IReadOnlyList<FacetBucket> Brands,
    IReadOnlyList<FacetBucket> Stores,
    IReadOnlyList<FacetBucket> PriceRanges,
    IReadOnlyList<FacetBucket> Ratings,
    IReadOnlyList<FacetBucket> Stock);

public record ScoreComponentDto(string Name, double Value, double Weight, double Contribution, string? Note);

public record ScoreBreakdownDto(double Total, IReadOnlyList<ScoreComponentDto> Components, double? DistanceKm);

public record RankedProductDto(ProductSummaryDto Product, double Score, ScoreBreakdownDto Breakdown);

public record FacetSearchResult(
    IReadOnlyList<RankedProductDto> Items, int Total, int Page, int PageSize,
    FacetsDto Facets, string? DidYouMean, string? EffectiveQuery,
    IReadOnlyList<SponsoredProductDto>? Sponsored = null);

/// <summary>Paid placement, always returned separately from organic Items and labelled "Sponsored".</summary>
public record SponsoredProductDto(ProductSummaryDto Product, Guid PlacementId, string Label = "Sponsored");

public record SuggestionDto(string Text, string Kind);

public record SavedSearchDto(Guid Id, string Name, FacetSearchRequest Query, bool AlertEnabled, DateTime CreatedAt);
public record CreateSavedSearchInput(string Name, FacetSearchRequest Query, bool AlertEnabled);
public record UpdateSavedSearchInput(string? Name, bool? AlertEnabled);

public record PriceWatchDto(
    Guid Id, Guid ProductId, string ProductName, string? ImageUrl,
    decimal BaselinePrice, decimal CurrentPrice, decimal? TargetPrice, bool IsActive, DateTime? LastNotifiedAt);
public record CreatePriceWatchInput(Guid ProductId, decimal? TargetPrice);

public record SetStoreLocationInput(double Latitude, double Longitude);

// ===== Service contracts =========================================================

public interface IProductVariantService
{
    Task<IReadOnlyList<VariantDto>> ListAsync(Guid productId, bool includeInactive = false, CancellationToken ct = default);
    Task<VariantDto> CreateAsync(Guid productId, UpsertVariantInput input, CancellationToken ct = default);
    Task<VariantDto> UpdateAsync(Guid variantId, UpsertVariantInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid variantId, CancellationToken ct = default);
    /// <summary>Variant-level stock operation; also moves Product.StockQuantity and writes an InventoryAdjustment.</summary>
    Task<VariantDto> AdjustStockAsync(Guid variantId, AdjustVariantStockInput input, CancellationToken ct = default);
    /// <summary>Adds the product to the cart via ICartService and remembers the chosen variant for that cart line.</summary>
    Task<CartDto> AddToCartAsync(AddVariantToCartInput input, CancellationToken ct = default);
}

public interface IStockReservationService
{
    /// <summary>Reserve every cart line for the configured hold window (replaces the caller's previous active holds).</summary>
    Task<CheckoutReservationDto> StartCheckoutAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReservationDto>> ListMineAsync(CancellationToken ct = default);
    Task ReleaseMineAsync(CancellationToken ct = default);
    /// <summary>Marks expired active reservations as Expired. Used by ReservationExpiryJob.</summary>
    Task<int> ExpireDueAsync(CancellationToken ct = default);
}

public interface IProductSubscriptionService
{
    Task<IReadOnlyList<SubscriptionDto>> ListMineAsync(CancellationToken ct = default);
    Task<SubscriptionDto> CreateAsync(CreateSubscriptionInput input, CancellationToken ct = default);
    Task<SubscriptionDto> UpdateAsync(Guid id, UpdateSubscriptionInput input, CancellationToken ct = default);
    Task<SubscriptionDto> PauseAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionDto> ResumeAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionDto> SkipNextAsync(Guid id, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
}

public interface IReorderService
{
    /// <summary>One-click reorder: adds every still-available line of a past order of the caller to the cart.</summary>
    Task<ReorderResultDto> ReorderAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>
/// Worker-side runner. Places orders for due subscriptions through the public IOrderService
/// (impersonating the subscriber via ShopPlusUserContext; see SubscriptionOrderPlacer).
/// </summary>
public interface ISubscriptionRunner
{
    Task<SubscriptionRunSummary> RunDueAsync(CancellationToken ct = default);
}

public interface IMarketplaceDiscoveryService
{
    Task<FacetSearchResult> SearchAsync(FacetSearchRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SuggestionDto>> SuggestAsync(string q, int limit = 8, CancellationToken ct = default);

    Task<IReadOnlyList<SavedSearchDto>> ListSavedSearchesAsync(CancellationToken ct = default);
    Task<SavedSearchDto> CreateSavedSearchAsync(CreateSavedSearchInput input, CancellationToken ct = default);
    Task<SavedSearchDto> UpdateSavedSearchAsync(Guid id, UpdateSavedSearchInput input, CancellationToken ct = default);
    Task DeleteSavedSearchAsync(Guid id, CancellationToken ct = default);

    Task RecordViewAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> RecentlyViewedAsync(int limit = 12, CancellationToken ct = default);

    Task<IReadOnlyList<PriceWatchDto>> ListPriceWatchesAsync(CancellationToken ct = default);
    Task<PriceWatchDto> CreatePriceWatchAsync(CreatePriceWatchInput input, CancellationToken ct = default);
    Task DeletePriceWatchAsync(Guid id, CancellationToken ct = default);

    /// <summary>Store owner sets the geo point used for distance ranking.</summary>
    Task SetMyStoreLocationAsync(SetStoreLocationInput input, CancellationToken ct = default);
}

/// <summary>Worker-side alert processing (no current user needed).</summary>
public interface IDiscoveryAlertProcessor
{
    Task<int> RunSavedSearchAlertsAsync(CancellationToken ct = default);
    Task<int> RunPriceDropsAsync(CancellationToken ct = default);
}
