namespace Pawzaroo.Application.Modules.Marketplace;

/// <summary>One cart line as seen by the pricing pipeline (prices already snapshotted by checkout).</summary>
public record PricingLine(Guid ProductId, Guid StoreId, Guid? CategoryId, Guid? VariantId, int Quantity, decimal UnitPrice)
{
    public decimal Total => UnitPrice * Quantity;
}

/// <summary>
/// Input of the pricing pipeline. <see cref="ComputeFees"/> maps (subtotal after discounts) to (shipping, tax, total) so
/// the pipeline can compute credits against the real payable amount without knowing marketplace options.
/// When <see cref="Commit"/> is false the pipeline is a read-only preview and adjusters must not mutate anything.
/// </summary>
public record OrderPricingRequest(
    Guid UserId,
    Guid OrderId,
    IReadOnlyList<PricingLine> Lines,
    decimal Subtotal,
    decimal CouponDiscount,
    bool CouponApplied,
    Func<decimal, (decimal Shipping, decimal Tax, decimal Total)> ComputeFees,
    int? RedeemLoyaltyPoints,
    string? GiftCardCode,
    decimal MinPayable,
    bool Commit);

public record AppliedAdjustment(string Kind, Guid? ReferenceId, decimal Amount, string Description, int? Points = null);

public enum PricingPhase
{
    /// <summary>Reduces the merchandise subtotal (promotions, bundles). Runs first.</summary>
    Merchandise = 0,
    /// <summary>Reduces the payable total after fees (loyalty points, gift cards). Runs second.</summary>
    Credit = 1
}

/// <summary>Mutable state threaded through the adjusters.</summary>
public class OrderPricingState
{
    public OrderPricingState(OrderPricingRequest request) { Request = request; }
    public OrderPricingRequest Request { get; }
    public decimal MerchandiseDiscount { get; set; }
    public bool FreeShipping { get; set; }
    /// <summary>Total payable before credits; set by the pipeline between the Merchandise and Credit phases.</summary>
    public decimal Payable { get; set; }
    public decimal CreditsApplied { get; set; }
    public List<AppliedAdjustment> Adjustments { get; } = new();
    /// <summary>How much more credit may still be applied while keeping the order at or above MinPayable.</summary>
    public decimal CreditCapacity => Math.Max(0m, Payable - CreditsApplied - Request.MinPayable);
    public decimal SubtotalAfterDiscounts => Request.Subtotal - Request.CouponDiscount - MerchandiseDiscount;
}

/// <summary>Pluggable pricing step. Implementations live in Infrastructure and are registered as IOrderPricingAdjuster.</summary>
public interface IOrderPricingAdjuster
{
    PricingPhase Phase { get; }
    int Order { get; }
    Task AdjustAsync(OrderPricingState state, CancellationToken ct = default);
}

/// <param name="MerchandiseDiscount">Promotion + bundle discount (added to Order.DiscountAmount alongside the coupon).</param>
/// <param name="CreditsApplied">Loyalty + gift card credits already subtracted from <see cref="Total"/>.</param>
public record OrderPricingResult(
    decimal MerchandiseDiscount, decimal ShippingFee, decimal Tax, decimal Total,
    decimal CreditsApplied, IReadOnlyList<AppliedAdjustment> Adjustments);

public interface IOrderPricingPipeline
{
    /// <summary>
    /// Runs all adjusters. With request.Commit the adjusters consume promotion usage / points / gift-card balance and
    /// stage OrderAppliedAdjustment rows on the shared DbContext (saved by the caller's SaveChanges).
    /// </summary>
    Task<OrderPricingResult> ApplyAsync(OrderPricingRequest request, CancellationToken ct = default);

    /// <summary>Order delivered: earn loyalty points, grant referral reward. Idempotent per order.</summary>
    Task OnOrderDeliveredAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Order cancelled/denied/refunded: reverse earned points, restore redeemed points and gift-card balance. Idempotent.</summary>
    Task OnOrderClosedAsync(Guid orderId, decimal? refundAmount, CancellationToken ct = default);
}

/// <summary>
/// Optional extension point: other modules (e.g. logistics zones/SLAs) contribute extra days to a delivery estimate.
/// All registered contributors are summed.
/// </summary>
public interface IDeliveryEtaContributor
{
    Task<DeliveryEtaContribution?> ContributeAsync(Guid storeId, string? destinationCity, CancellationToken ct = default);
}

public record DeliveryEtaContribution(double ExtraMinDays, double ExtraMaxDays, string? Note);
