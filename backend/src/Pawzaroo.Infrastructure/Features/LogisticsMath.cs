using Pawzaroo.Application.Modules.LogisticsPlus;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>Pure shipping-quote arithmetic and peak-hour matching (no I/O) so it can be unit tested.</summary>
public static class ShippingQuoteMath
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static int DaysToMask(IEnumerable<int> days)
    {
        var mask = 0;
        foreach (var d in days) if (d is >= 0 and <= 6) mask |= 1 << d;
        return mask;
    }

    public static IReadOnlyList<int> MaskToDays(int mask) =>
        Enumerable.Range(0, 7).Where(d => (mask & (1 << d)) != 0).ToList();

    /// <summary>
    /// True when the local date/time falls inside the weekly window. Days are the days the window STARTS on
    /// (bit 0 = Sunday). An overnight window (start &gt; end, e.g. 22:00-02:00) started on the previous day
    /// during its after-midnight part. start == end is an empty window.
    /// </summary>
    public static bool IsInWindow(int daysMask, TimeSpan start, TimeSpan end, DateTime local)
    {
        if (daysMask == 0 || start == end) return false;
        var tod = local.TimeOfDay;
        var today = (int)local.DayOfWeek;
        var yesterday = (today + 6) % 7;
        bool Has(int d) => (daysMask & (1 << d)) != 0;

        if (start < end) return Has(today) && tod >= start && tod < end;
        // overnight
        if (tod >= start) return Has(today);
        if (tod < end) return Has(yesterday);
        return false;
    }

    /// <summary>Surcharge = fixed + percent of the base fee, 2dp.</summary>
    public static decimal Surcharge(decimal baseFee, decimal fixedAmount, decimal percent) =>
        Round(Math.Max(fixedAmount, 0m) + Math.Max(baseFee, 0m) * Math.Max(percent, 0m) / 100m);

    /// <summary>
    /// Fee for one zone. Free shipping (subtotal at/above a positive threshold) waives the base fee AND any peak surcharge.
    /// </summary>
    public static (decimal Base, decimal Peak, bool FreeApplied, decimal Total) Compute(
        decimal baseFee, decimal? freeThreshold, decimal subtotal, decimal peakSurcharge)
    {
        var b = Round(Math.Max(baseFee, 0m));
        var p = Round(Math.Max(peakSurcharge, 0m));
        if (freeThreshold is > 0m && subtotal >= freeThreshold.Value) return (0m, 0m, true, 0m);
        return (b, p, false, b + p);
    }

    public static bool CityMatches(string? citiesCsv, string? city)
    {
        if (string.IsNullOrWhiteSpace(citiesCsv) || string.IsNullOrWhiteSpace(city)) return false;
        var c = city.Trim();
        return citiesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase));
    }

    public static (DateTime Earliest, DateTime Latest) Eta(DateTime at, int minHours, int maxHours)
    {
        var lo = Math.Max(minHours, 0);
        var hi = Math.Max(maxHours, lo);
        return (at.AddHours(lo), at.AddHours(hi));
    }
}

/// <summary>Pure SLA evaluation.</summary>
public static class SlaMath
{
    public const double AtRiskFraction = 0.75;

    private static SlaStatus One(DateTime start, DateTime? doneAt, DateTime now, int maxHours)
    {
        var due = start.AddHours(maxHours);
        if (doneAt.HasValue) return doneAt.Value > due ? SlaStatus.Breached : SlaStatus.OnTrack;
        if (now > due) return SlaStatus.Breached;
        return now >= start.AddHours(maxHours * AtRiskFraction) ? SlaStatus.AtRisk : SlaStatus.OnTrack;
    }

    /// <summary>Both targets are measured from <paramref name="start"/> (order paid / placed time).</summary>
    public static (SlaStatus Ship, SlaStatus Deliver, SlaStatus Overall) Evaluate(
        DateTime start, DateTime? shippedAt, DateTime? deliveredAt, DateTime now, int maxHoursToShip, int maxHoursToDeliver)
    {
        var ship = One(start, shippedAt, now, maxHoursToShip);
        var deliver = One(start, deliveredAt, now, maxHoursToDeliver);
        return (ship, deliver, (SlaStatus)Math.Max((int)ship, (int)deliver));
    }
}

/// <summary>Pure partial-refund allocation.</summary>
public static class PartialRefundMath
{
    /// <summary>
    /// Amount refunded for <paramref name="requestQty"/> units of a line whose net (after pro-rata discount) is
    /// <paramref name="itemNet"/>. The last remaining units take whatever is left so per-line refunds always sum to the net.
    /// </summary>
    public static decimal LineRefund(decimal itemNet, int lineQty, int alreadyQty, decimal alreadyAmount, int requestQty)
    {
        if (lineQty <= 0) throw new ArgumentException("Line quantity must be positive.", nameof(lineQty));
        if (requestQty <= 0) throw new ArgumentException("Quantity must be positive.", nameof(requestQty));
        var remaining = lineQty - alreadyQty;
        if (requestQty > remaining) throw new ArgumentException("Quantity exceeds the refundable quantity.", nameof(requestQty));
        var leftover = Math.Max(itemNet - alreadyAmount, 0m);
        if (requestQty == remaining) return LedgerMath.Round(leftover);
        return LedgerMath.Round(Math.Min(itemNet * requestQty / lineQty, leftover));
    }
}

public static class MetricsMath
{
    /// <summary>numerator / denominator, 0 when the denominator is not positive, clamped to [0,1], 4dp.</summary>
    public static decimal Rate(int numerator, int denominator)
    {
        if (denominator <= 0 || numerator <= 0) return 0m;
        return Math.Round(Math.Min((decimal)numerator / denominator, 1m), 4, MidpointRounding.AwayFromZero);
    }
}

public static class ReturnPickupMath
{
    /// <summary>Allowed status moves. Requested -> Scheduled happens on assignment; Failed is allowed from any open state.</summary>
    public static bool CanTransition(Domain.Store.ReturnPickupStatus from, Domain.Store.ReturnPickupStatus to)
    {
        return (from, to) switch
        {
            (Domain.Store.ReturnPickupStatus.Requested, Domain.Store.ReturnPickupStatus.Scheduled) => true,
            (Domain.Store.ReturnPickupStatus.Scheduled, Domain.Store.ReturnPickupStatus.PickedUp) => true,
            (Domain.Store.ReturnPickupStatus.PickedUp, Domain.Store.ReturnPickupStatus.InTransit) => true,
            (Domain.Store.ReturnPickupStatus.PickedUp, Domain.Store.ReturnPickupStatus.ReceivedByStore) => true,
            (Domain.Store.ReturnPickupStatus.InTransit, Domain.Store.ReturnPickupStatus.ReceivedByStore) => true,
            (Domain.Store.ReturnPickupStatus.Scheduled, Domain.Store.ReturnPickupStatus.Failed) => true,
            (Domain.Store.ReturnPickupStatus.PickedUp, Domain.Store.ReturnPickupStatus.Failed) => true,
            (Domain.Store.ReturnPickupStatus.InTransit, Domain.Store.ReturnPickupStatus.Failed) => true,
            (Domain.Store.ReturnPickupStatus.Failed, Domain.Store.ReturnPickupStatus.Scheduled) => true, // re-assigned
            _ => false
        };
    }
}
