using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.ShopPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>8.5 — product variants, checkout stock reservations, subscribe-and-save, one-click reorder.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shop-plus")]
[Authorize]
public class ShopPlusController : ControllerBase
{
    private readonly IProductVariantService _variants;
    private readonly IStockReservationService _reservations;
    private readonly IProductSubscriptionService _subs;
    private readonly IReorderService _reorder;

    public ShopPlusController(IProductVariantService variants, IStockReservationService reservations,
        IProductSubscriptionService subs, IReorderService reorder)
    {
        _variants = variants;
        _reservations = reservations;
        _subs = subs;
        _reorder = reorder;
    }

    // ---- variants -------------------------------------------------------------

    [HttpGet("products/{productId:guid}/variants")]
    [AllowAnonymous]
    public Task<IReadOnlyList<VariantDto>> Variants(Guid productId, CancellationToken ct)
        => _variants.ListAsync(productId, false, ct);

    /// <summary>Store owner view including inactive variants.</summary>
    [HttpGet("products/{productId:guid}/variants/manage")]
    public Task<IReadOnlyList<VariantDto>> VariantsManage(Guid productId, CancellationToken ct)
        => _variants.ListAsync(productId, true, ct);

    [HttpPost("products/{productId:guid}/variants")]
    public Task<VariantDto> CreateVariant(Guid productId, [FromBody] UpsertVariantInput input, CancellationToken ct)
        => _variants.CreateAsync(productId, input, ct);

    [HttpPut("variants/{variantId:guid}")]
    public Task<VariantDto> UpdateVariant(Guid variantId, [FromBody] UpsertVariantInput input, CancellationToken ct)
        => _variants.UpdateAsync(variantId, input, ct);

    [HttpDelete("variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid variantId, CancellationToken ct)
    {
        await _variants.DeleteAsync(variantId, ct);
        return NoContent();
    }

    [HttpPost("variants/{variantId:guid}/stock")]
    public Task<VariantDto> AdjustStock(Guid variantId, [FromBody] AdjustVariantStockInput input, CancellationToken ct)
        => _variants.AdjustStockAsync(variantId, input, ct);

    [HttpPost("cart/variant-items")]
    public Task<CartDto> AddVariantToCart([FromBody] AddVariantToCartInput input, CancellationToken ct)
        => _variants.AddToCartAsync(input, ct);

    // ---- reservations ---------------------------------------------------------

    [HttpPost("reservations/start-checkout")]
    public Task<CheckoutReservationDto> StartCheckout(CancellationToken ct) => _reservations.StartCheckoutAsync(ct);

    [HttpGet("reservations")]
    public Task<IReadOnlyList<ReservationDto>> MyReservations(CancellationToken ct) => _reservations.ListMineAsync(ct);

    [HttpPost("reservations/release")]
    public async Task<IActionResult> Release(CancellationToken ct)
    {
        await _reservations.ReleaseMineAsync(ct);
        return NoContent();
    }

    // ---- subscriptions --------------------------------------------------------

    [HttpGet("subscriptions")]
    public Task<IReadOnlyList<SubscriptionDto>> Subscriptions(CancellationToken ct) => _subs.ListMineAsync(ct);

    [HttpPost("subscriptions")]
    public Task<SubscriptionDto> Subscribe([FromBody] CreateSubscriptionInput input, CancellationToken ct)
        => _subs.CreateAsync(input, ct);

    [HttpPut("subscriptions/{id:guid}")]
    public Task<SubscriptionDto> UpdateSubscription(Guid id, [FromBody] UpdateSubscriptionInput input, CancellationToken ct)
        => _subs.UpdateAsync(id, input, ct);

    [HttpPost("subscriptions/{id:guid}/pause")]
    public Task<SubscriptionDto> Pause(Guid id, CancellationToken ct) => _subs.PauseAsync(id, ct);

    [HttpPost("subscriptions/{id:guid}/resume")]
    public Task<SubscriptionDto> Resume(Guid id, CancellationToken ct) => _subs.ResumeAsync(id, ct);

    [HttpPost("subscriptions/{id:guid}/skip")]
    public Task<SubscriptionDto> Skip(Guid id, CancellationToken ct) => _subs.SkipNextAsync(id, ct);

    [HttpDelete("subscriptions/{id:guid}")]
    public async Task<IActionResult> CancelSubscription(Guid id, CancellationToken ct)
    {
        await _subs.CancelAsync(id, ct);
        return NoContent();
    }

    // ---- reorder --------------------------------------------------------------

    [HttpPost("orders/{orderId:guid}/reorder")]
    public Task<ReorderResultDto> Reorder(Guid orderId, CancellationToken ct) => _reorder.ReorderAsync(orderId, ct);
}
