using FluentAssertions;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Domain.Store;
using Xunit;

namespace Pawzaroo.Tests.Unit.Marketplace;

public class LoyaltyPlusLogicTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid P1 = Guid.NewGuid();
    private static readonly Guid P2 = Guid.NewGuid();

    private static PromotionRules.Candidate Promo(PromotionType type, decimal value = 10, bool stackable = false,
        PromotionScope scope = PromotionScope.Storewide, Guid? scopeRef = null, decimal min = 0, int? limit = null, int used = 0,
        DateTime? start = null, DateTime? end = null, bool active = true, int buy = 0, int get = 0) =>
        new(Guid.NewGuid(), "p", type, scope, scopeRef, value, buy, get, min, start, end, limit, used, stackable, active);

    private static IReadOnlyList<PricingLine> Lines() => new[]
    {
        new PricingLine(P1, StoreA, null, null, 2, 50m),   // 100
        new PricingLine(P2, Guid.NewGuid(), null, null, 1, 100m) // 100
    };

    // ---- promotions ------------------------------------------------------------------

    [Fact]
    public void Percent_off_storewide_applies_to_whole_cart()
    {
        var e = PromotionRules.Evaluate(Promo(PromotionType.PercentOff, 10), Lines(), Now);
        e!.Discount.Should().Be(20m);
    }

    [Fact]
    public void Scoped_promotion_only_counts_matching_lines_and_min_subtotal()
    {
        var p = Promo(PromotionType.PercentOff, 50, scope: PromotionScope.Product, scopeRef: P1, min: 150m);
        PromotionRules.Evaluate(p, Lines(), Now).Should().BeNull(); // 100 < 150
        var q = Promo(PromotionType.PercentOff, 50, scope: PromotionScope.Product, scopeRef: P1, min: 100m);
        PromotionRules.Evaluate(q, Lines(), Now)!.Discount.Should().Be(50m);
    }

    [Fact]
    public void Inactive_expired_or_exhausted_promotions_are_not_eligible()
    {
        PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 5, active: false), Lines(), Now).Should().BeNull();
        PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 5, end: Now.AddDays(-1)), Lines(), Now).Should().BeNull();
        PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 5, start: Now.AddDays(1)), Lines(), Now).Should().BeNull();
        PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 5, limit: 3, used: 3), Lines(), Now).Should().BeNull();
    }

    [Fact]
    public void Fixed_off_never_exceeds_eligible_subtotal()
    {
        PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 500), Lines(), Now)!.Discount.Should().Be(200m);
    }

    [Fact]
    public void Buy_x_get_y_gives_free_units_per_full_group()
    {
        var lines = new[] { new PricingLine(P1, StoreA, null, null, 7, 10m) };
        var e = PromotionRules.Evaluate(Promo(PromotionType.BuyXGetY, buy: 2, get: 1), lines, Now);
        e!.Discount.Should().Be(20m); // 7 / 3 = 2 groups -> 2 free units
    }

    [Fact]
    public void Stackable_promotions_combine_non_stackable_competes()
    {
        var a = PromotionRules.Evaluate(Promo(PromotionType.PercentOff, 10, stackable: true), Lines(), Now)!; // 20
        var b = PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 5, stackable: true), Lines(), Now)!;     // 5
        var big = PromotionRules.Evaluate(Promo(PromotionType.PercentOff, 30), Lines(), Now)!;                // 60
        var small = PromotionRules.Evaluate(Promo(PromotionType.FixedOff, 10), Lines(), Now)!;                // 10

        PromotionRules.Select(new[] { a, b }, false, 5m, 200m).Sum(x => x.Discount).Should().Be(25m);
        PromotionRules.Select(new[] { a, b, big }, false, 5m, 200m).Sum(x => x.Discount).Should().Be(60m);   // 60 > 25
        PromotionRules.Select(new[] { a, b, small }, false, 5m, 200m).Sum(x => x.Discount).Should().Be(25m); // 25 > 10
    }

    [Fact]
    public void Coupon_blocks_non_stackable_promotions_and_total_is_capped()
    {
        var a = PromotionRules.Evaluate(Promo(PromotionType.PercentOff, 10, stackable: true), Lines(), Now)!;
        var big = PromotionRules.Evaluate(Promo(PromotionType.PercentOff, 30), Lines(), Now)!;
        PromotionRules.Select(new[] { a, big }, true, 5m, 200m).Sum(x => x.Discount).Should().Be(20m);
        PromotionRules.Select(new[] { a }, false, 5m, 8m).Sum(x => x.Discount).Should().Be(8m);
    }

    [Fact]
    public void Free_shipping_promotion_sets_flag_once()
    {
        var f1 = PromotionRules.Evaluate(Promo(PromotionType.FreeShipping, stackable: true), Lines(), Now)!;
        var f2 = PromotionRules.Evaluate(Promo(PromotionType.FreeShipping, stackable: true), Lines(), Now)!;
        PromotionRules.Select(new[] { f1, f2 }, false, 5m, 200m).Count(x => x.FreeShipping).Should().Be(1);
    }

    // ---- bundles ---------------------------------------------------------------------

    [Fact]
    public void Bundle_price_fixed_or_percent_never_exceeds_list()
    {
        BundlePricing.BundlePrice(100m, 80m, null).Should().Be(80m);
        BundlePricing.BundlePrice(100m, 150m, null).Should().Be(100m);
        BundlePricing.BundlePrice(100m, null, 15m).Should().Be(85m);
        BundlePricing.DiscountPerBundle(100m, 80m, null).Should().Be(20m);
    }

    [Fact]
    public void Bundle_stock_and_cart_coverage_use_the_scarcest_component()
    {
        BundlePricing.MaxBundlesByStock(new[] { (1, 10), (2, 5) }).Should().Be(2);
        BundlePricing.MaxBundlesByStock(new[] { (1, 10), (2, 1) }).Should().Be(0);
        BundlePricing.BundlesCoveredByCart(3, new[] { (1, 3), (2, 4) }).Should().Be(2);
        BundlePricing.BundlesCoveredByCart(1, new[] { (1, 3), (2, 4) }).Should().Be(1);
        BundlePricing.BundlesCoveredByCart(2, new[] { (1, 0) }).Should().Be(0);
    }

    // ---- gift cards -------------------------------------------------------------------

    [Fact]
    public void Gift_card_math_redeem_refund_and_status()
    {
        GiftCardMath.AmountToRedeem(50m, 30m).Should().Be(30m);
        GiftCardMath.AmountToRedeem(20m, 30m).Should().Be(20m);
        GiftCardMath.AmountToRedeem(20m, -1m).Should().Be(0m);
        GiftCardMath.AfterRedeem(20m, 20m).Should().Be((0m, GiftCardStatus.Depleted));
        GiftCardMath.AfterRedeem(50m, 20m).Should().Be((30m, GiftCardStatus.Active));
        GiftCardMath.AfterRefund(30m, 50m, 60m).Should().Be(60m);
    }

    [Fact]
    public void Gift_card_codes_normalize_hash_and_expire()
    {
        GiftCardMath.Hash("abcd-1234").Should().Be(GiftCardMath.Hash("ABCD1234"));
        var code = GiftCardMath.GenerateCode();
        code.Should().MatchRegex("^[A-Z2-9]{4}(-[A-Z2-9]{4}){3}$");
        GiftCardMath.Hash(code).Should().HaveLength(64);
        GiftCardMath.IsUsable(GiftCardStatus.Active, 10m, Now.AddDays(1), Now).Should().BeTrue();
        GiftCardMath.IsUsable(GiftCardStatus.Active, 10m, Now.AddDays(-1), Now).Should().BeFalse();
        GiftCardMath.IsUsable(GiftCardStatus.Cancelled, 10m, null, Now).Should().BeFalse();
        GiftCardMath.IsUsable(GiftCardStatus.Active, 0m, null, Now).Should().BeFalse();
    }

    // ---- loyalty -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, LoyaltyTier.Bronze)]
    [InlineData(999, LoyaltyTier.Bronze)]
    [InlineData(1000, LoyaltyTier.Silver)]
    [InlineData(5000, LoyaltyTier.Gold)]
    [InlineData(15000, LoyaltyTier.Platinum)]
    public void Tier_thresholds(int lifetime, LoyaltyTier tier) => LoyaltyRules.TierFor(lifetime).Should().Be(tier);

    [Fact]
    public void Earn_uses_tier_multiplier_and_redeem_is_capped()
    {
        LoyaltyRules.PointsForOrder(100m, 1m, LoyaltyTier.Bronze).Should().Be(100);
        LoyaltyRules.PointsForOrder(100m, 1m, LoyaltyTier.Gold).Should().Be(125);
        LoyaltyRules.PointsForOrder(-5m, 1m, LoyaltyTier.Gold).Should().Be(0);
        // payable 100, 1 point = 0.01, max 50% => 5000 points; balance 300 limits
        LoyaltyRules.MaxRedeemablePoints(300, 100m, 0.01m, 50m).Should().Be(300);
        LoyaltyRules.MaxRedeemablePoints(99999, 100m, 0.01m, 50m).Should().Be(5000);
        LoyaltyRules.PointsValue(250, 0.01m).Should().Be(2.5m);
    }

    [Fact]
    public void Expiry_is_fifo_and_idempotent()
    {
        var lots = new[]
        {
            new LoyaltyRules.EarnLot(100, Now.AddDays(-10)), // expired
            new LoyaltyRules.EarnLot(100, Now.AddDays(20)),
        };
        LoyaltyRules.PointsToExpire(lots, 0, 200, Now).Should().Be(100);
        LoyaltyRules.PointsToExpire(lots, 30, 170, Now).Should().Be(70);   // spent points eat the oldest lot first
        LoyaltyRules.PointsToExpire(lots, 100, 100, Now).Should().Be(0);   // old lot fully spent
        LoyaltyRules.PointsToExpire(lots, 100, 100, Now).Should().Be(0);
        LoyaltyRules.PointsToExpire(lots, 100 + 0, 100, Now).Should().Be(0);
        // after expiring 100 (consumed = 100), nothing further expires
        LoyaltyRules.PointsToExpire(lots, 100, 100, Now).Should().Be(0);
        // never more than the balance
        LoyaltyRules.PointsToExpire(lots, 0, 40, Now).Should().Be(40);
    }

    // ---- referral ------------------------------------------------------------------------

    [Fact]
    public void Referral_eligibility_and_reward()
    {
        var owner = Guid.NewGuid(); var me = Guid.NewGuid();
        ReferralRules.CanRedeem(owner, me, false, false).Should().BeTrue();
        ReferralRules.CanRedeem(owner, owner, false, false).Should().BeFalse();
        ReferralRules.CanRedeem(owner, me, true, false).Should().BeFalse();
        ReferralRules.CanRedeem(owner, me, false, true).Should().BeFalse();
        ReferralRules.ShouldReward(1, null).Should().BeTrue();
        ReferralRules.ShouldReward(2, null).Should().BeFalse();
        ReferralRules.ShouldReward(1, Now).Should().BeFalse();
        ReferralRules.NormalizeCode(" ab12 ").Should().Be("AB12");
    }

    // ---- replenishment / delivery estimate -------------------------------------------------

    [Fact]
    public void Replenishment_estimate_from_repeat_purchases()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // 2 units bought on day 0, 2 more on day 30: rate 2/30 per day -> 2 units last 30 days -> day 60
        var runOut = ReplenishmentEstimator.EstimateRunOut(new[]
        {
            new ReplenishmentEstimator.Purchase(t0, 2), new ReplenishmentEstimator.Purchase(t0.AddDays(30), 2)
        });
        runOut.Should().Be(t0.AddDays(60));
    }

    [Fact]
    public void Replenishment_single_purchase_uses_default_and_is_clamped()
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ReplenishmentEstimator.EstimateRunOut(new[] { new ReplenishmentEstimator.Purchase(t0, 1) }).Should().Be(t0.AddDays(30));
        ReplenishmentEstimator.EstimateRunOut(new[] { new ReplenishmentEstimator.Purchase(t0, 100) }).Should().Be(t0.AddDays(180));
        ReplenishmentEstimator.EstimateRunOut(Array.Empty<ReplenishmentEstimator.Purchase>()).Should().BeNull();
    }

    [Fact]
    public void Delivery_eta_range_is_ordered_and_includes_contributions()
    {
        var (min, max) = DeliveryEtaMath.Range(3.0);
        min.Should().BeGreaterThan(0);
        max.Should().BeGreaterThan(min);
        var (min2, max2) = DeliveryEtaMath.Range(3.0, 1, 2);
        min2.Should().BeGreaterThanOrEqualTo(min);
        max2.Should().BeGreaterThan(max);
        DeliveryEtaMath.Range(0).MaxDays.Should().BeGreaterThan(DeliveryEtaMath.Range(0).MinDays);
    }
}
