using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.LoyaltyPlus;

/// <summary>
/// Config keys (all optional): Loyalty:PointsPerCurrencyUnit (1), Loyalty:ValuePerPoint (0.01),
/// Loyalty:MaxRedeemPercent (50), Loyalty:PointsExpiryDays (365), Loyalty:ReferrerRewardPoints (500),
/// Loyalty:ReferredBonusPoints (250), Marketplace:LowStockDefaultThreshold (5).
/// </summary>
public class LoyaltyPlusOptions
{
    public decimal PointsPerCurrencyUnit { get; set; } = 1m;
    public decimal ValuePerPoint { get; set; } = 0.01m;
    public decimal MaxRedeemPercent { get; set; } = 50m;
    public int PointsExpiryDays { get; set; } = 365;
    public int ReferrerRewardPoints { get; set; } = 500;
    public int ReferredBonusPoints { get; set; } = 250;
    public int LowStockDefaultThreshold { get; set; } = 5;

    public static LoyaltyPlusOptions From(IConfiguration c)
    {
        var o = new LoyaltyPlusOptions();
        decimal D(string k, decimal d) => decimal.TryParse(c[k], System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
        int I(string k, int d) => int.TryParse(c[k], out var v) ? v : d;
        o.PointsPerCurrencyUnit = D("Loyalty:PointsPerCurrencyUnit", o.PointsPerCurrencyUnit);
        o.ValuePerPoint = D("Loyalty:ValuePerPoint", o.ValuePerPoint);
        o.MaxRedeemPercent = D("Loyalty:MaxRedeemPercent", o.MaxRedeemPercent);
        o.PointsExpiryDays = I("Loyalty:PointsExpiryDays", o.PointsExpiryDays);
        o.ReferrerRewardPoints = I("Loyalty:ReferrerRewardPoints", o.ReferrerRewardPoints);
        o.ReferredBonusPoints = I("Loyalty:ReferredBonusPoints", o.ReferredBonusPoints);
        o.LowStockDefaultThreshold = I("Marketplace:LowStockDefaultThreshold", o.LowStockDefaultThreshold);
        return o;
    }
}

/// <summary>Runs the adjusters in phase/order and stages OrderAppliedAdjustment rows (called once by OrderService.CheckoutAsync).</summary>
public class OrderPricingPipeline : IOrderPricingPipeline
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IOrderPricingAdjuster> _adjusters;
    private readonly LoyaltyService _loyalty;
    private readonly GiftCardService _giftCards;

    public OrderPricingPipeline(ApplicationDbContext db, IEnumerable<IOrderPricingAdjuster> adjusters,
        LoyaltyService loyalty, GiftCardService giftCards)
    {
        _db = db;
        _adjusters = adjusters;
        _loyalty = loyalty;
        _giftCards = giftCards;
    }

    public async Task<OrderPricingResult> ApplyAsync(OrderPricingRequest request, CancellationToken ct = default)
    {
        var state = new OrderPricingState(request);

        foreach (var a in _adjusters.Where(a => a.Phase == PricingPhase.Merchandise).OrderBy(a => a.Order))
            await a.AdjustAsync(state, ct);
        state.MerchandiseDiscount = Math.Min(state.MerchandiseDiscount, Math.Max(0m, request.Subtotal - request.CouponDiscount));

        var (shipping, tax, total) = request.ComputeFees(Math.Max(0m, state.SubtotalAfterDiscounts));
        if (state.FreeShipping && shipping > 0m)
        {
            total -= shipping;
            state.Adjustments.Add(new AppliedAdjustment("freeShipping", null, shipping, "Free shipping"));
            shipping = 0m;
        }
        state.Payable = total;

        foreach (var a in _adjusters.Where(a => a.Phase == PricingPhase.Credit).OrderBy(a => a.Order))
            await a.AdjustAsync(state, ct);

        var finalTotal = Math.Max(0m, total - state.CreditsApplied);

        if (request.Commit)
        {
            foreach (var adj in state.Adjustments)
                _db.Set<OrderAppliedAdjustment>().Add(new OrderAppliedAdjustment
                {
                    OrderId = request.OrderId, Kind = adj.Kind, ReferenceId = adj.ReferenceId,
                    Amount = adj.Amount, Description = adj.Description, Points = adj.Points
                });
        }

        return new OrderPricingResult(state.MerchandiseDiscount, shipping, tax, finalTotal,
            state.CreditsApplied, state.Adjustments);
    }

    public async Task OnOrderDeliveredAsync(Guid orderId, CancellationToken ct = default)
    {
        try { await _loyalty.EarnForOrderAsync(orderId, ct); }
        catch (OperationCanceledException) { throw; }
        catch { /* never break the order flow; the worker backfill retries */ }
    }

    public async Task OnOrderClosedAsync(Guid orderId, decimal? refundAmount, CancellationToken ct = default)
    {
        try
        {
            var order = await _db.Orders.AsNoTracking().Where(o => o.Id == orderId)
                .Select(o => new { o.Total, o.UserId }).FirstOrDefaultAsync(ct);
            if (order is null) return;
            var full = refundAmount is null || refundAmount >= order.Total;

            await _loyalty.ReverseForOrderAsync(orderId, refundAmount, order.Total, full, ct);
            if (full)
            {
                await _giftCards.RefundForOrderAsync(orderId, ct);
                await ReversePromotionsAsync(orderId, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* best effort, idempotent: next close/refund call retries */ }
    }

    private async Task ReversePromotionsAsync(Guid orderId, CancellationToken ct)
    {
        var rows = await _db.Set<OrderAppliedAdjustment>()
            .Where(a => a.OrderId == orderId && a.ReversedAt == null && a.Kind == "promotion" && a.ReferenceId != null)
            .ToListAsync(ct);
        foreach (var r in rows)
        {
            var pid = r.ReferenceId!.Value;
            await _db.Set<Promotion>().Where(p => p.Id == pid && p.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount - 1), ct);
            r.ReversedAt = DateTime.UtcNow;
        }
        if (rows.Count > 0) await _db.SaveChangesAsync(ct);
    }
}

// ---------------------------------------------------------------------------------------------------------------
// Adjusters
// ---------------------------------------------------------------------------------------------------------------

public class PromotionAdjuster : IOrderPricingAdjuster
{
    private readonly ApplicationDbContext _db;
    public PromotionAdjuster(ApplicationDbContext db) => _db = db;
    public PricingPhase Phase => PricingPhase.Merchandise;
    public int Order => 10;

    public async Task AdjustAsync(OrderPricingState state, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Set<Promotion>().AsNoTracking()
            .Where(p => p.IsActive && (p.StartsAt == null || p.StartsAt <= now) && (p.EndsAt == null || p.EndsAt >= now))
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        var evaluated = rows.Select(ToCandidate)
            .Select(c => PromotionRules.Evaluate(c, state.Request.Lines, now))
            .Where(e => e is not null).Select(e => e!).ToList();
        if (evaluated.Count == 0) return;

        var baseAfterCoupon = Math.Max(0m, state.Request.Subtotal - state.Request.CouponDiscount - state.MerchandiseDiscount);
        var shippingFee = state.Request.ComputeFees(baseAfterCoupon).Shipping;
        var chosen = PromotionRules.Select(evaluated, state.Request.CouponApplied, shippingFee, baseAfterCoupon);

        foreach (var e in chosen)
        {
            if (state.Request.Commit)
            {
                var id = e.Promotion.Id;
                var rows2 = await _db.Set<Promotion>()
                    .Where(p => p.Id == id && (p.UsageLimit == null || p.UsedCount < p.UsageLimit))
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsedCount, p => p.UsedCount + 1), ct);
                if (rows2 == 0) continue; // limit reached concurrently
            }
            if (e.Discount > 0m)
            {
                state.MerchandiseDiscount += e.Discount;
                state.Adjustments.Add(new AppliedAdjustment("promotion", e.Promotion.Id, e.Discount, e.Promotion.Name));
            }
            if (e.FreeShipping)
            {
                state.FreeShipping = true;
                state.Adjustments.Add(new AppliedAdjustment("promotion", e.Promotion.Id, 0m, e.Promotion.Name + " (free shipping)"));
            }
        }
    }

    internal static PromotionRules.Candidate ToCandidate(Promotion p) => new(
        p.Id, p.Name, p.Type, p.Scope, p.ScopeRefId, p.Value, p.BuyQuantity, p.GetQuantity, p.MinSubtotal,
        p.StartsAt, p.EndsAt, p.UsageLimit, p.UsedCount, p.Stackable, p.IsActive);
}

public class BundleAdjuster : IOrderPricingAdjuster
{
    private readonly ApplicationDbContext _db;
    public BundleAdjuster(ApplicationDbContext db) => _db = db;
    public PricingPhase Phase => PricingPhase.Merchandise;
    public int Order => 20;

    public async Task AdjustAsync(OrderPricingState state, CancellationToken ct = default)
    {
        var uid = state.Request.UserId;
        var marks = await _db.Set<BundleCartDiscount>().Where(b => b.UserId == uid).ToListAsync(ct);
        if (marks.Count == 0) return;

        var bundleIds = marks.Select(m => m.BundleId).ToList();
        var bundles = await _db.Set<ProductBundle>().AsNoTracking().Include(b => b.Items)
            .Where(b => bundleIds.Contains(b.Id) && b.IsActive).ToListAsync(ct);

        // Remaining cart quantity per (product, variant); each bundle consumes what it covers.
        var pool = state.Request.Lines.GroupBy(l => (l.ProductId, l.VariantId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var unit = state.Request.Lines.GroupBy(l => (l.ProductId, l.VariantId))
            .ToDictionary(g => g.Key, g => g.First().UnitPrice);

        foreach (var m in marks)
        {
            var bundle = bundles.FirstOrDefault(b => b.Id == m.BundleId);
            if (bundle is null || bundle.Items.Count == 0) continue;

            var covered = BundlePricing.BundlesCoveredByCart(m.Quantity,
                bundle.Items.Select(i => (i.Quantity, pool.GetValueOrDefault((i.ProductId, i.VariantId)))));
            if (covered <= 0) continue;

            var comps = bundle.Items.Select(i => new BundlePricing.Component(
                i.ProductId, i.VariantId, i.Quantity, unit.GetValueOrDefault((i.ProductId, i.VariantId)))).ToList();
            var perBundle = BundlePricing.DiscountPerBundle(BundlePricing.ListPrice(comps), bundle.Price, bundle.DiscountPercent);
            var discount = Math.Round(perBundle * covered, 2, MidpointRounding.AwayFromZero);
            foreach (var i in bundle.Items) pool[(i.ProductId, i.VariantId)] -= i.Quantity * covered;
            if (discount <= 0m) continue;

            state.MerchandiseDiscount += discount;
            state.Adjustments.Add(new AppliedAdjustment("bundle", bundle.Id, discount, $"{bundle.Name} x{covered}"));
        }

        if (state.Request.Commit) _db.Set<BundleCartDiscount>().RemoveRange(marks);
    }
}

public class LoyaltyRedemptionAdjuster : IOrderPricingAdjuster
{
    private readonly ApplicationDbContext _db;
    private readonly LoyaltyPlusOptions _opt;
    public LoyaltyRedemptionAdjuster(ApplicationDbContext db, LoyaltyPlusOptions opt) { _db = db; _opt = opt; }
    public PricingPhase Phase => PricingPhase.Credit;
    public int Order => 10;

    public async Task AdjustAsync(OrderPricingState state, CancellationToken ct = default)
    {
        var requested = state.Request.RedeemLoyaltyPoints ?? 0;
        if (requested <= 0) return;

        var uid = state.Request.UserId;
        var balance = await _db.Set<LoyaltyAccount>().AsNoTracking().Where(a => a.UserId == uid)
            .Select(a => (int?)a.Balance).FirstOrDefaultAsync(ct) ?? 0;
        var cap = LoyaltyRules.MaxRedeemablePoints(balance, state.Payable, _opt.ValuePerPoint, _opt.MaxRedeemPercent);
        var byCapacity = (int)Math.Floor(state.CreditCapacity / _opt.ValuePerPoint);
        var pts = Math.Min(requested, Math.Min(cap, byCapacity));
        if (pts <= 0) return;

        if (state.Request.Commit)
        {
            var rows = await _db.Set<LoyaltyAccount>().Where(a => a.UserId == uid && a.Balance >= pts)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance - pts)
                    .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);
            if (rows == 0) return;
            _db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
            {
                UserId = uid, Type = LoyaltyTransactionType.Redeem, Points = -pts, OrderId = state.Request.OrderId,
                Note = "Redeemed at checkout"
            });
        }

        var value = LoyaltyRules.PointsValue(pts, _opt.ValuePerPoint);
        state.CreditsApplied += value;
        state.Adjustments.Add(new AppliedAdjustment("loyalty", null, value, $"{pts} loyalty points", pts));
    }
}

public class GiftCardAdjuster : IOrderPricingAdjuster
{
    private readonly ApplicationDbContext _db;
    public GiftCardAdjuster(ApplicationDbContext db) => _db = db;
    public PricingPhase Phase => PricingPhase.Credit;
    public int Order => 20;

    public async Task AdjustAsync(OrderPricingState state, CancellationToken ct = default)
    {
        var code = state.Request.GiftCardCode;
        if (string.IsNullOrWhiteSpace(code)) return;

        var hash = GiftCardMath.Hash(code);
        var card = await _db.Set<GiftCard>().AsNoTracking().FirstOrDefaultAsync(g => g.CodeHash == hash, ct);
        var now = DateTime.UtcNow;
        if (card is null || !GiftCardMath.IsUsable(card.Status, card.Balance, card.ExpiresAt, now))
            throw new ValidationException(new Dictionary<string, string[]> { ["giftCardCode"] = new[] { "Gift card is invalid, expired or empty." } });

        var amount = GiftCardMath.AmountToRedeem(card.Balance, state.CreditCapacity);
        if (amount <= 0m) return;

        if (state.Request.Commit)
        {
            var id = card.Id;
            var rows = await _db.Set<GiftCard>().Where(g => g.Id == id && g.Status == GiftCardStatus.Active && g.Balance >= amount)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Balance, g => g.Balance - amount)
                    .SetProperty(g => g.UpdatedAt, (DateTime?)now), ct);
            if (rows == 0)
                throw new ConflictException("Gift card balance changed; please try again.");
            await _db.Set<GiftCard>().Where(g => g.Id == id && g.Balance <= 0m)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GiftCardStatus.Depleted), ct);
            _db.Set<GiftCardTransaction>().Add(new GiftCardTransaction
            {
                GiftCardId = id, Type = GiftCardTransactionType.Redeem, Amount = -amount, OrderId = state.Request.OrderId
            });
        }

        state.CreditsApplied += amount;
        state.Adjustments.Add(new AppliedAdjustment("giftcard", card.Id, amount, "Gift card " + Mask(card.Code)));
    }

    internal static string Mask(string code) => code.Length <= 4 ? code : "****-" + code[^4..];
}
