using System.Security.Cryptography;
using System.Text;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.Marketplace;

// Pure, side-effect free rules (unit tested). Services in Infrastructure just load data and call these.

public static class PromotionRules
{
    public record Candidate(
        Guid Id, string Name, PromotionType Type, PromotionScope Scope, Guid? ScopeRefId, decimal Value,
        int BuyQuantity, int GetQuantity, decimal MinSubtotal, DateTime? StartsAt, DateTime? EndsAt,
        int? UsageLimit, int UsedCount, bool Stackable, bool IsActive);

    public record Evaluation(Candidate Promotion, decimal Discount, bool FreeShipping);

    public static bool InWindow(Candidate p, DateTime now) =>
        p.IsActive && (p.StartsAt is null || p.StartsAt <= now) && (p.EndsAt is null || p.EndsAt >= now)
        && (p.UsageLimit is null || p.UsedCount < p.UsageLimit);

    public static IReadOnlyList<PricingLine> InScope(Candidate p, IEnumerable<PricingLine> lines) => p.Scope switch
    {
        PromotionScope.Store => lines.Where(l => l.StoreId == p.ScopeRefId).ToList(),
        PromotionScope.Category => lines.Where(l => l.CategoryId == p.ScopeRefId).ToList(),
        PromotionScope.Product => lines.Where(l => l.ProductId == p.ScopeRefId).ToList(),
        _ => lines.ToList()
    };

    /// <summary>Returns null when not eligible, otherwise the discount it would give.</summary>
    public static Evaluation? Evaluate(Candidate p, IReadOnlyList<PricingLine> lines, DateTime now)
    {
        if (!InWindow(p, now)) return null;
        var scoped = InScope(p, lines);
        if (scoped.Count == 0) return null;
        var eligible = scoped.Sum(l => l.Total);
        if (eligible <= 0 || eligible < p.MinSubtotal) return null;

        switch (p.Type)
        {
            case PromotionType.PercentOff:
                var pct = Math.Clamp(p.Value, 0m, 100m);
                var d = Math.Round(eligible * pct / 100m, 2, MidpointRounding.AwayFromZero);
                return d > 0 ? new Evaluation(p, d, false) : null;
            case PromotionType.FixedOff:
                var f = Math.Min(Math.Max(p.Value, 0m), eligible);
                return f > 0 ? new Evaluation(p, f, false) : null;
            case PromotionType.FreeShipping:
                return new Evaluation(p, 0m, true);
            case PromotionType.BuyXGetY:
                if (p.BuyQuantity <= 0 || p.GetQuantity <= 0) return null;
                var group = p.BuyQuantity + p.GetQuantity;
                decimal free = 0m;
                foreach (var l in scoped)
                    free += (l.Quantity / group) * p.GetQuantity * l.UnitPrice;
                return free > 0 ? new Evaluation(p, Math.Min(free, eligible), false) : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Stacking: all stackable promotions apply together. A non-stackable promotion applies alone (it excludes every
    /// other promotion) and is skipped entirely when a coupon was used. When both kinds exist the better deal wins.
    /// The total is capped to <paramref name="maxDiscount"/> (never discount below zero).
    /// </summary>
    public static IReadOnlyList<Evaluation> Select(IReadOnlyList<Evaluation> evaluated, bool couponApplied,
        decimal shippingFee, decimal maxDiscount)
    {
        decimal Worth(Evaluation e) => e.Discount + (e.FreeShipping ? shippingFee : 0m);

        var stackable = evaluated.Where(e => e.Promotion.Stackable).ToList();
        var best = couponApplied ? null : evaluated.Where(e => !e.Promotion.Stackable).OrderByDescending(Worth).FirstOrDefault();

        List<Evaluation> chosen;
        if (best is not null && Worth(best) > stackable.Sum(Worth)) chosen = new List<Evaluation> { best };
        else chosen = stackable;

        var result = new List<Evaluation>();
        var remaining = Math.Max(0m, maxDiscount);
        var freeShipSeen = false;
        foreach (var e in chosen.OrderByDescending(Worth))
        {
            var take = Math.Min(e.Discount, remaining);
            var fs = e.FreeShipping && !freeShipSeen;
            if (take <= 0 && !fs) continue;
            remaining -= take;
            freeShipSeen |= fs;
            result.Add(new Evaluation(e.Promotion, take, fs));
        }
        return result;
    }
}

public static class BundlePricing
{
    public record Component(Guid ProductId, Guid? VariantId, int QuantityPerBundle, decimal UnitPrice);

    public static decimal ListPrice(IEnumerable<Component> components) =>
        components.Sum(c => c.UnitPrice * c.QuantityPerBundle);

    /// <summary>Bundle price per bundle (fixed price wins over percent; never above list price).</summary>
    public static decimal BundlePrice(decimal listPrice, decimal? fixedPrice, decimal? discountPercent)
    {
        if (fixedPrice is { } fp) return Math.Min(Math.Max(0m, fp), listPrice);
        if (discountPercent is { } pct)
            return listPrice - Math.Round(listPrice * Math.Clamp(pct, 0m, 100m) / 100m, 2, MidpointRounding.AwayFromZero);
        return listPrice;
    }

    public static decimal DiscountPerBundle(decimal listPrice, decimal? fixedPrice, decimal? discountPercent) =>
        listPrice - BundlePrice(listPrice, fixedPrice, discountPercent);

    /// <summary>How many whole bundles the available stock can supply (stock check across components).</summary>
    public static int MaxBundlesByStock(IEnumerable<(int QuantityPerBundle, int Stock)> components)
    {
        var list = components.ToList();
        if (list.Count == 0) return 0;
        return list.Min(c => c.QuantityPerBundle <= 0 ? 0 : Math.Max(0, c.Stock) / c.QuantityPerBundle);
    }

    /// <summary>Bundles still fully backed by cart quantities; the bundle discount is only granted for these.</summary>
    public static int BundlesCoveredByCart(int requestedBundles, IEnumerable<(int QuantityPerBundle, int CartQuantity)> components)
    {
        var list = components.ToList();
        if (list.Count == 0 || requestedBundles <= 0) return 0;
        var covered = list.Min(c => c.QuantityPerBundle <= 0 ? 0 : Math.Max(0, c.CartQuantity) / c.QuantityPerBundle);
        return Math.Min(requestedBundles, covered);
    }
}

public static class GiftCardMath
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Normalize(string? code) =>
        new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string Hash(string? code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)))).ToLowerInvariant();

    /// <summary>XXXX-XXXX-XXXX-XXXX from a CSPRNG (no ambiguous characters).</summary>
    public static string GenerateCode()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    public static bool IsUsable(GiftCardStatus status, decimal balance, DateTime? expiresAt, DateTime now) =>
        status == GiftCardStatus.Active && balance > 0m && (expiresAt is null || expiresAt > now);

    /// <summary>Amount to draw: never more than balance or what is payable, never negative.</summary>
    public static decimal AmountToRedeem(decimal balance, decimal payable) =>
        Math.Max(0m, Math.Min(Math.Round(balance, 2), Math.Round(payable, 2)));

    public static (decimal NewBalance, GiftCardStatus Status) AfterRedeem(decimal balance, decimal redeemed) =>
        (Math.Round(balance - redeemed, 2), balance - redeemed <= 0m ? GiftCardStatus.Depleted : GiftCardStatus.Active);

    /// <summary>Refund restores balance but never above the initial amount.</summary>
    public static decimal AfterRefund(decimal balance, decimal refunded, decimal initialAmount) =>
        Math.Min(initialAmount, Math.Round(balance + refunded, 2));

    public const decimal MinPurchaseAmount = 100m;
    public const decimal MaxPurchaseAmount = 50000m;

    /// <summary>Amount limits for a customer purchase request (admin issue has its own, wider limit).</summary>
    public static bool IsValidPurchaseAmount(decimal amount) => amount >= MinPurchaseAmount && amount <= MaxPurchaseAmount;

    /// <summary>Customers only see the full code once the card is active; pending/cancelled cards show a masked code.</summary>
    public static string DisplayCode(string code, GiftCardStatus status) =>
        status is GiftCardStatus.Pending or GiftCardStatus.Cancelled
            ? (code.Length <= 4 ? "****" : "****-" + code[^4..])
            : code;
}

public static class LoyaltyRules
{
    public static LoyaltyTier TierFor(int lifetimePoints) => lifetimePoints switch
    {
        >= 15000 => LoyaltyTier.Platinum,
        >= 5000 => LoyaltyTier.Gold,
        >= 1000 => LoyaltyTier.Silver,
        _ => LoyaltyTier.Bronze
    };

    public static decimal TierMultiplier(LoyaltyTier t) => t switch
    {
        LoyaltyTier.Platinum => 1.5m, LoyaltyTier.Gold => 1.25m, LoyaltyTier.Silver => 1.1m, _ => 1m
    };

    public static int PointsForOrder(decimal orderTotal, decimal pointsPerCurrencyUnit, LoyaltyTier tier) =>
        orderTotal <= 0m ? 0 : (int)Math.Floor(orderTotal * pointsPerCurrencyUnit * TierMultiplier(tier));

    public static decimal PointsValue(int points, decimal valuePerPoint) =>
        Math.Round(Math.Max(0, points) * valuePerPoint, 2, MidpointRounding.AwayFromZero);

    /// <summary>Highest points the user may spend: limited by balance, by max % of payable and by what is payable.</summary>
    public static int MaxRedeemablePoints(int balance, decimal payable, decimal valuePerPoint, decimal maxPercentOfPayable)
    {
        if (balance <= 0 || payable <= 0m || valuePerPoint <= 0m) return 0;
        var maxValue = payable * Math.Clamp(maxPercentOfPayable, 0m, 100m) / 100m;
        var byValue = (int)Math.Floor(maxValue / valuePerPoint);
        return Math.Max(0, Math.Min(balance, byValue));
    }

    public record EarnLot(int Points, DateTime? ExpiresAt);

    /// <summary>
    /// FIFO expiry: net points consumed so far (redeemed/expired/reversed minus restored) are charged against the
    /// oldest lots first; whatever remains of lots already past their expiry is returned (capped at the balance).
    /// Re-running right after expiring yields 0 because Expire rows count as consumed.
    /// </summary>
    public static int PointsToExpire(IEnumerable<EarnLot> earns, int netConsumed, int balance, DateTime now)
    {
        var consumed = Math.Max(0, netConsumed);
        var expire = 0;
        foreach (var lot in earns.OrderBy(l => l.ExpiresAt ?? DateTime.MaxValue))
        {
            var take = Math.Min(lot.Points, consumed);
            consumed -= take;
            var left = lot.Points - take;
            if (lot.ExpiresAt is { } e && e <= now) expire += left;
        }
        return Math.Max(0, Math.Min(expire, balance));
    }
}

public static class ReferralRules
{
    public static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>Can the redeemer claim a referral code owned by ownerId?</summary>
    public static bool CanRedeem(Guid ownerId, Guid redeemerId, bool redeemerAlreadyReferred, bool redeemerHasPriorOrders) =>
        ownerId != redeemerId && !redeemerAlreadyReferred && !redeemerHasPriorOrders;

    /// <summary>Reward when the referred user's first order is delivered, once.</summary>
    public static bool ShouldReward(int deliveredOrderCountIncludingThis, DateTime? alreadyRewardedAt) =>
        alreadyRewardedAt is null && deliveredOrderCountIncludingThis == 1;
}

public static class ReplenishmentEstimator
{
    public const double MinDays = 7, MaxDays = 180, DefaultDaysPerUnit = 30;

    public record Purchase(DateTime At, int Quantity);

    /// <summary>
    /// Estimates when the buyer runs out. With 2+ purchases: consumption rate = units bought before the last purchase
    /// / days between first and last purchase; run-out = last purchase + last quantity / rate. With one purchase a
    /// default of one unit per DefaultDaysPerUnit days is assumed. The span is clamped to [MinDays, MaxDays].
    /// </summary>
    public static DateTime? EstimateRunOut(IEnumerable<Purchase> purchases)
    {
        var list = purchases.Where(p => p.Quantity > 0).OrderBy(p => p.At).ToList();
        if (list.Count == 0) return null;
        var last = list[^1];
        double days;
        var span = (last.At - list[0].At).TotalDays;
        if (list.Count >= 2 && span >= 1)
        {
            var consumedUnits = list.Take(list.Count - 1).Sum(p => p.Quantity);
            var perDay = consumedUnits / span;
            days = perDay > 0 ? last.Quantity / perDay : DefaultDaysPerUnit * last.Quantity;
        }
        else days = DefaultDaysPerUnit * last.Quantity;
        days = Math.Clamp(days, MinDays, MaxDays);
        return last.At.AddDays(days);
    }
}

public static class DeliveryEtaMath
{
    /// <summary>Range from an average duration: min = 80%, max = 130% rounded to whole days, at least 1 day apart.</summary>
    public static (int MinDays, int MaxDays) Range(double averageDays, double extraMin = 0, double extraMax = 0)
    {
        var avg = Math.Max(0.5, averageDays);
        var min = (int)Math.Floor(avg * 0.8 + extraMin);
        var max = (int)Math.Ceiling(avg * 1.3 + extraMax);
        min = Math.Max(1, min);
        if (max <= min) max = min + 1;
        return (min, max);
    }
}
