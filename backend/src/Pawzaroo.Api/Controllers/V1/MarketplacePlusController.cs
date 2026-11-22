using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Application.Modules.Marketplace.Dtos;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Marketplace expansion 1.4 / 2.4: bundles, stock alerts, replenishment, store comparison, delivery promise,
/// availability check, loyalty/referral, gift cards, promotions and sponsored placements.
/// Authorization is enforced inside the services (store ownership / Settings.Edit).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/marketplace-plus")]
[Authorize]
public class MarketplacePlusController : ControllerBase
{
    private readonly IBundleService _bundles;
    private readonly IStockAlertService _stock;
    private readonly IReplenishmentService _replenish;
    private readonly IStoreComparisonService _compare;
    private readonly IDeliveryPromiseService _delivery;
    private readonly ICartAvailabilityService _availability;
    private readonly ICartPricingPreviewService _preview;
    private readonly ILoyaltyService _loyalty;
    private readonly IGiftCardService _giftCards;
    private readonly IPromotionService _promotions;
    private readonly ISponsoredPlacementService _sponsored;

    public MarketplacePlusController(IBundleService bundles, IStockAlertService stock, IReplenishmentService replenish,
        IStoreComparisonService compare, IDeliveryPromiseService delivery, ICartAvailabilityService availability,
        ICartPricingPreviewService preview, ILoyaltyService loyalty, IGiftCardService giftCards,
        IPromotionService promotions, ISponsoredPlacementService sponsored)
    {
        _bundles = bundles; _stock = stock; _replenish = replenish; _compare = compare; _delivery = delivery;
        _availability = availability; _preview = preview; _loyalty = loyalty; _giftCards = giftCards;
        _promotions = promotions; _sponsored = sponsored;
    }

    // ---- bundles ----------------------------------------------------------------------
    [HttpGet("bundles"), AllowAnonymous]
    public Task<IReadOnlyList<BundleDto>> Bundles([FromQuery] int take, CancellationToken ct) => _bundles.ListActiveAsync(take <= 0 ? 12 : take, ct);

    [HttpGet("bundles/product/{productId:guid}"), AllowAnonymous]
    public Task<IReadOnlyList<BundleDto>> BundlesForProduct(Guid productId, CancellationToken ct) => _bundles.ListForProductAsync(productId, ct);

    [HttpGet("bundles/store/{storeId:guid}"), AllowAnonymous]
    public Task<IReadOnlyList<BundleDto>> BundlesForStore(Guid storeId, [FromQuery] bool includeInactive, CancellationToken ct)
        => _bundles.ListForStoreAsync(storeId, includeInactive, ct);

    [HttpGet("bundles/{id:guid}"), AllowAnonymous]
    public Task<BundleDto> Bundle(Guid id, CancellationToken ct) => _bundles.GetAsync(id, ct);

    [HttpPost("bundles")]
    public Task<BundleDto> CreateBundle([FromBody] UpsertBundleInput input, CancellationToken ct) => _bundles.CreateAsync(input, ct);

    [HttpPut("bundles/{id:guid}")]
    public Task<BundleDto> UpdateBundle(Guid id, [FromBody] UpsertBundleInput input, CancellationToken ct) => _bundles.UpdateAsync(id, input, ct);

    [HttpDelete("bundles/{id:guid}")]
    public async Task<IActionResult> DeleteBundle(Guid id, CancellationToken ct) { await _bundles.DeleteAsync(id, ct); return NoContent(); }

    [HttpPost("bundles/{id:guid}/add-to-cart")]
    public Task<CartDto> AddBundleToCart(Guid id, [FromBody] AddBundleToCartInput input, CancellationToken ct) => _bundles.AddToCartAsync(id, input, ct);

    // ---- stock alerts / replenishment -------------------------------------------------------
    [HttpGet("stock/store/{storeId:guid}")]
    public Task<IReadOnlyList<StockThresholdDto>> StockForStore(Guid storeId, CancellationToken ct) => _stock.ListForStoreAsync(storeId, ct);

    [HttpPut("stock/threshold")]
    public async Task<IActionResult> SetThreshold([FromBody] SetStockThresholdInput input, CancellationToken ct) { await _stock.SetThresholdAsync(input, ct); return NoContent(); }

    [HttpGet("replenishment")]
    public Task<IReadOnlyList<ReplenishmentReminderDto>> Replenishment(CancellationToken ct) => _replenish.ListMineAsync(ct);

    [HttpPost("replenishment/{id:guid}/dismiss")]
    public async Task<IActionResult> DismissReplenishment(Guid id, CancellationToken ct) { await _replenish.DismissAsync(id, ct); return NoContent(); }

    // ---- comparison / delivery promise / availability / pricing preview ---------------------------
    [HttpGet("stores/compare"), AllowAnonymous]
    public Task<StoreComparisonDto> Compare([FromQuery] Guid[] ids, CancellationToken ct) => _compare.CompareAsync(ids, ct);

    [HttpGet("delivery-promise/store/{storeId:guid}"), AllowAnonymous]
    public Task<DeliveryPromiseDto> DeliveryForStore(Guid storeId, [FromQuery] string? city, CancellationToken ct) => _delivery.EstimateAsync(storeId, city, ct);

    [HttpGet("delivery-promise/cart")]
    public Task<CartDeliveryPromiseDto?> DeliveryForCart([FromQuery] string? city, CancellationToken ct) => _delivery.EstimateForCartAsync(city, ct);

    /// <summary>GET api/v1/cart/availability — verifies stock/active/store/variant per line before payment.</summary>
    [HttpGet("~/api/v{version:apiVersion}/cart/availability")]
    public Task<CartAvailabilityDto> Availability(CancellationToken ct) => _availability.CheckAsync(ct);

    [HttpPost("pricing-preview")]
    public Task<CartPricingPreviewDto> PricingPreview([FromBody] CartPricingPreviewRequest input, CancellationToken ct) => _preview.PreviewAsync(input, ct);

    // ---- loyalty / referral -----------------------------------------------------------------
    [HttpGet("loyalty")]
    public Task<LoyaltyAccountDto> Loyalty(CancellationToken ct) => _loyalty.GetMineAsync(ct);

    [HttpGet("loyalty/history")]
    public Task<PageResult<LoyaltyTransactionDto>> LoyaltyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _loyalty.HistoryAsync(page, pageSize, ct);

    [HttpGet("loyalty/redeem-preview")]
    public Task<LoyaltyRedeemPreviewDto> RedeemPreview([FromQuery] decimal payable, CancellationToken ct) => _loyalty.PreviewRedeemAsync(payable, ct);

    [HttpPost("loyalty/referral/redeem")]
    public async Task<IActionResult> RedeemReferral([FromBody] RedeemReferralInput input, CancellationToken ct) { await _loyalty.RedeemReferralCodeAsync(input.Code, ct); return NoContent(); }

    [HttpPost("loyalty/adjust")]
    public async Task<IActionResult> AdjustLoyalty([FromBody] AdjustLoyaltyInput input, CancellationToken ct) { await _loyalty.AdjustAsync(input, ct); return NoContent(); }

    // ---- gift cards -------------------------------------------------------------------------
    [HttpPost("giftcards/check")]
    public Task<GiftCardBalanceDto> CheckGiftCard([FromBody] CheckGiftCardInput input, CancellationToken ct) => _giftCards.CheckBalanceAsync(input.Code, ct);

    [HttpGet("giftcards")]
    public Task<IReadOnlyList<GiftCardDto>> GiftCards([FromQuery] int take = 100, CancellationToken ct = default) => _giftCards.ListAsync(take, ct);

    [HttpPost("giftcards")]
    public Task<GiftCardDto> IssueGiftCard([FromBody] IssueGiftCardInput input, CancellationToken ct) => _giftCards.IssueAsync(input, ct);

    /// <summary>Customer purchase request (pending until an admin confirms payment).</summary>
    [HttpPost("giftcards/request")]
    public Task<GiftCardDto> RequestGiftCard([FromBody] RequestGiftCardPurchaseInput input, CancellationToken ct) => _giftCards.RequestPurchaseAsync(input, ct);

    [HttpGet("giftcards/mine")]
    public Task<IReadOnlyList<GiftCardDto>> MyGiftCards(CancellationToken ct) => _giftCards.ListMyPurchasesAsync(ct);

    [HttpPost("giftcards/{id:guid}/activate")]
    public Task<GiftCardDto> ActivateGiftCard(Guid id, CancellationToken ct) => _giftCards.ActivateAsync(id, ct);

    [HttpPost("giftcards/{id:guid}/cancel")]
    public async Task<IActionResult> CancelGiftCard(Guid id, CancellationToken ct) { await _giftCards.CancelAsync(id, ct); return NoContent(); }

    // ---- promotions -------------------------------------------------------------------------
    [HttpGet("promotions")]
    public Task<IReadOnlyList<PromotionDto>> Promotions(CancellationToken ct) => _promotions.ListAsync(ct);

    [HttpGet("promotions/active"), AllowAnonymous]
    public Task<IReadOnlyList<PromotionDto>> ActivePromotions(CancellationToken ct) => _promotions.ListActivePublicAsync(ct);

    [HttpPost("promotions")]
    public Task<PromotionDto> CreatePromotion([FromBody] UpsertPromotionInput input, CancellationToken ct) => _promotions.CreateAsync(input, ct);

    [HttpPut("promotions/{id:guid}")]
    public Task<PromotionDto> UpdatePromotion(Guid id, [FromBody] UpsertPromotionInput input, CancellationToken ct) => _promotions.UpdateAsync(id, input, ct);

    [HttpDelete("promotions/{id:guid}")]
    public async Task<IActionResult> DeletePromotion(Guid id, CancellationToken ct) { await _promotions.DeleteAsync(id, ct); return NoContent(); }

    // ---- sponsored placements ---------------------------------------------------------------
    [HttpGet("sponsored")]
    public Task<IReadOnlyList<SponsoredPlacementDto>> Sponsored([FromQuery] Guid? storeId, CancellationToken ct) => _sponsored.ListAsync(storeId, ct);

    [HttpPost("sponsored")]
    public Task<SponsoredPlacementDto> CreateSponsored([FromBody] UpsertSponsoredPlacementInput input, CancellationToken ct) => _sponsored.CreateAsync(input, ct);

    [HttpPut("sponsored/{id:guid}")]
    public Task<SponsoredPlacementDto> UpdateSponsored(Guid id, [FromBody] UpsertSponsoredPlacementInput input, CancellationToken ct) => _sponsored.UpdateAsync(id, input, ct);

    public record ReviewInput(bool Approve);

    [HttpPost("sponsored/{id:guid}/review")]
    public async Task<IActionResult> ReviewSponsored(Guid id, [FromBody] ReviewInput input, CancellationToken ct) { await _sponsored.ReviewAsync(id, input.Approve, ct); return NoContent(); }

    [HttpDelete("sponsored/{id:guid}")]
    public async Task<IActionResult> DeleteSponsored(Guid id, CancellationToken ct) { await _sponsored.DeleteAsync(id, ct); return NoContent(); }
}
