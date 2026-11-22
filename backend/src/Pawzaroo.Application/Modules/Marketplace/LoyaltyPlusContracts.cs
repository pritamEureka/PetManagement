using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.Marketplace;

// ===== Bundles =================================================================

public record BundleItemDto(
    Guid ProductId, string ProductName, string? ImageUrl, Guid? VariantId, string? VariantLabel,
    int Quantity, decimal UnitPrice, int Stock);

public record BundleDto(
    Guid Id, Guid StoreId, string StoreName, string Name, string? Description,
    decimal? Price, decimal? DiscountPercent, bool IsActive,
    decimal ListPrice, decimal BundlePrice, decimal Savings, int MaxAvailable,
    IReadOnlyList<BundleItemDto> Items);

public record BundleItemInput(Guid ProductId, Guid? VariantId, int Quantity);

public record UpsertBundleInput(
    Guid? StoreId, string Name, string? Description, decimal? Price, decimal? DiscountPercent,
    bool IsActive, IReadOnlyList<BundleItemInput> Items);

public record AddBundleToCartInput(int Quantity = 1);

public interface IBundleService
{
    Task<IReadOnlyList<BundleDto>> ListActiveAsync(int take = 12, CancellationToken ct = default);
    Task<IReadOnlyList<BundleDto>> ListForProductAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<BundleDto>> ListForStoreAsync(Guid storeId, bool includeInactive, CancellationToken ct = default);
    Task<BundleDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<BundleDto> CreateAsync(UpsertBundleInput input, CancellationToken ct = default);
    Task<BundleDto> UpdateAsync(Guid id, UpsertBundleInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Adds the component lines to the cart and records a BundleCartDiscount (discount applied at checkout).</summary>
    Task<CartDto> AddToCartAsync(Guid id, AddBundleToCartInput input, CancellationToken ct = default);
}

// ===== Stock alerts / replenishment ================================================

public record StockThresholdDto(Guid ProductId, Guid? VariantId, string ProductName, string? VariantSku,
    int Stock, int Threshold, DateTime? LastAlertedAt, bool IsLow);
public record SetStockThresholdInput(Guid ProductId, Guid? VariantId, int Threshold);

public interface IStockAlertService
{
    /// <summary>Worker entry: notifies store owners about low stock (de-duplicated). Returns alerts sent.</summary>
    Task<int> RunLowStockAlertsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockThresholdDto>> ListForStoreAsync(Guid storeId, CancellationToken ct = default);
    Task SetThresholdAsync(SetStockThresholdInput input, CancellationToken ct = default);
}

public record ReplenishmentReminderDto(
    Guid Id, Guid ProductId, string ProductName, string? ImageUrl, decimal Price, bool InStock,
    DateTime ExpectedRunOutAt, int DaysLeft);

public interface IReplenishmentService
{
    /// <summary>Worker entry: recompute run-out estimates from delivered orders and notify buyers once. Returns reminders sent.</summary>
    Task<int> RunAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReplenishmentReminderDto>> ListMineAsync(CancellationToken ct = default);
    Task DismissAsync(Guid id, CancellationToken ct = default);
}

// ===== Store comparison / delivery promise / availability ==============================

public record StoreComparisonItemDto(
    Guid StoreId, string Name, string? City, string? LogoUrl,
    decimal? Rating, int ReviewCount, double? AvgFulfilmentDays, int DeliveredOrders,
    double? ReturnRatePercent, decimal? PriceIndex, int ComparableProducts, decimal ShippingFee, decimal FreeShippingThreshold,
    int ActiveProducts);

public record StoreComparisonDto(IReadOnlyList<StoreComparisonItemDto> Stores);

public interface IStoreComparisonService
{
    /// <summary>Compares 2-4 approved stores. PriceIndex: average price ratio vs the cross-store median on common products (100 = median).</summary>
    Task<StoreComparisonDto> CompareAsync(IReadOnlyList<Guid> storeIds, CancellationToken ct = default);
}

public record DeliveryPromiseDto(
    Guid StoreId, int MinDays, int MaxDays, DateTime EarliestDate, DateTime LatestDate,
    string Basis, int SampleSize, IReadOnlyList<string> Notes);

public record CartDeliveryPromiseDto(DateTime EarliestDate, DateTime LatestDate, IReadOnlyList<DeliveryPromiseDto> PerStore);

public interface IDeliveryPromiseService
{
    Task<DeliveryPromiseDto> EstimateAsync(Guid storeId, string? destinationCity, CancellationToken ct = default);
    Task<CartDeliveryPromiseDto?> EstimateForCartAsync(string? destinationCity, CancellationToken ct = default);
}

public record AvailabilityProblemDto(Guid CartItemId, Guid ProductId, string ProductName, string Code, string Message, int Available);
public record CartAvailabilityDto(bool Ok, IReadOnlyList<AvailabilityProblemDto> Problems);

public interface ICartAvailabilityService
{
    Task<CartAvailabilityDto> CheckAsync(CancellationToken ct = default);
}

// ===== Loyalty / referral =====================================================

public record LoyaltyAccountDto(
    int Balance, int LifetimePoints, LoyaltyTier Tier, LoyaltyTier? NextTier, int PointsToNextTier,
    decimal ValuePerPoint, decimal BalanceValue, string ReferralCode, int Referrals, int RewardedReferrals);

public record LoyaltyTransactionDto(Guid Id, LoyaltyTransactionType Type, int Points, Guid? OrderId, string? Note,
    DateTime CreatedAt, DateTime? ExpiresAt);

public record LoyaltyRedeemPreviewDto(int MaxPoints, decimal MaxValue, int Balance, decimal ValuePerPoint);

public record RedeemReferralInput(string Code);
public record AdjustLoyaltyInput(Guid UserId, int Points, string? Note);

public interface ILoyaltyService
{
    Task<LoyaltyAccountDto> GetMineAsync(CancellationToken ct = default);
    Task<PageResult<LoyaltyTransactionDto>> HistoryAsync(int page, int pageSize, CancellationToken ct = default);
    Task<LoyaltyRedeemPreviewDto> PreviewRedeemAsync(decimal payable, CancellationToken ct = default);
    Task RedeemReferralCodeAsync(string code, CancellationToken ct = default);
    Task AdjustAsync(AdjustLoyaltyInput input, CancellationToken ct = default);
    /// <summary>Worker entry: expires lapsed point lots. Returns accounts touched.</summary>
    Task<int> ExpirePointsAsync(CancellationToken ct = default);
}

// ===== Gift cards ===============================================================

public record GiftCardDto(Guid Id, string Code, decimal InitialAmount, decimal Balance, string Currency,
    string? RecipientEmail, string? Message, DateTime? ExpiresAt, GiftCardStatus Status, DateTime CreatedAt);
public record GiftCardBalanceDto(string MaskedCode, decimal Balance, string Currency, DateTime? ExpiresAt, bool Usable);
public record IssueGiftCardInput(decimal Amount, string? Currency, string? RecipientEmail, string? Message, DateTime? ExpiresAt);
public record CheckGiftCardInput(string Code);
public record RequestGiftCardPurchaseInput(decimal Amount, string? RecipientEmail, string? Message);

public interface IGiftCardService
{
    /// <summary>Admin issue (Settings.Edit). Online purchase is not supported: the payment flow is order-bound.</summary>
    Task<GiftCardDto> IssueAsync(IssueGiftCardInput input, CancellationToken ct = default);
    Task<IReadOnlyList<GiftCardDto>> ListAsync(int take, CancellationToken ct = default);
    Task CancelAsync(Guid id, CancellationToken ct = default);
    Task<GiftCardBalanceDto> CheckBalanceAsync(string code, CancellationToken ct = default);
    /// <summary>
    /// Customer "request purchase": creates a PENDING card (balance 0, code hidden). Online payment is not supported because
    /// payments are order-bound; an admin activates the card after confirming payment offline/COD.
    /// </summary>
    Task<GiftCardDto> RequestPurchaseAsync(RequestGiftCardPurchaseInput input, CancellationToken ct = default);
    Task<IReadOnlyList<GiftCardDto>> ListMyPurchasesAsync(CancellationToken ct = default);
    /// <summary>Admin: Pending -> Active (balance = initial amount, Issue transaction). Idempotent.</summary>
    Task<GiftCardDto> ActivateAsync(Guid id, CancellationToken ct = default);
}

// ===== Promotions ==================================================================

public record PromotionDto(Guid Id, string Name, PromotionType Type, PromotionScope Scope, Guid? ScopeRefId, decimal Value,
    int BuyQuantity, int GetQuantity, decimal MinSubtotal, DateTime? StartsAt, DateTime? EndsAt,
    int? UsageLimit, int UsedCount, bool Stackable, bool IsActive, DateTime CreatedAt);
public record UpsertPromotionInput(string Name, PromotionType Type, PromotionScope Scope, Guid? ScopeRefId, decimal Value,
    int BuyQuantity, int GetQuantity, decimal MinSubtotal, DateTime? StartsAt, DateTime? EndsAt,
    int? UsageLimit, bool Stackable, bool IsActive);

public interface IPromotionService
{
    Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PromotionDto>> ListActivePublicAsync(CancellationToken ct = default);
    Task<PromotionDto> CreateAsync(UpsertPromotionInput input, CancellationToken ct = default);
    Task<PromotionDto> UpdateAsync(Guid id, UpsertPromotionInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// ===== Cart pricing preview =======================================================

public record CartPricingPreviewRequest(string? CouponCode, int? RedeemLoyaltyPoints, string? GiftCardCode);
public record AppliedAdjustmentDto(string Kind, string Description, decimal Amount, int? Points);
public record CartPricingPreviewDto(decimal Subtotal, decimal CouponDiscount, decimal MerchandiseDiscount,
    decimal ShippingFee, decimal Tax, decimal CreditsApplied, decimal Total, IReadOnlyList<AppliedAdjustmentDto> Adjustments);

public interface ICartPricingPreviewService
{
    Task<CartPricingPreviewDto> PreviewAsync(CartPricingPreviewRequest request, CancellationToken ct = default);
}

// ===== Sponsored placements ========================================================

public record SponsoredPlacementDto(Guid Id, Guid ProductId, string ProductName, Guid StoreId, string StoreName,
    DateTime StartsAt, DateTime EndsAt, decimal? Budget, decimal Spent, decimal BidWeight, bool IsActive,
    SponsoredPlacementStatus Status, int Impressions, DateTime CreatedAt);
public record UpsertSponsoredPlacementInput(Guid ProductId, DateTime StartsAt, DateTime EndsAt, decimal? Budget, decimal? BidWeight, bool? IsActive);

public interface ISponsoredPlacementService
{
    /// <summary>Admin: all placements (Settings.Edit). Store owner: their store's placements.</summary>
    Task<IReadOnlyList<SponsoredPlacementDto>> ListAsync(Guid? storeId, CancellationToken ct = default);
    /// <summary>Store owners create Pending requests; admins (Settings.Edit) create Approved placements.</summary>
    Task<SponsoredPlacementDto> CreateAsync(UpsertSponsoredPlacementInput input, CancellationToken ct = default);
    Task<SponsoredPlacementDto> UpdateAsync(Guid id, UpsertSponsoredPlacementInput input, CancellationToken ct = default);
    Task ReviewAsync(Guid id, bool approve, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
