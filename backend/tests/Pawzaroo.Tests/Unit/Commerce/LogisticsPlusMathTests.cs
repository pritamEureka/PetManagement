using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.Commerce;

public class LogisticsPlusMathTests
{
    private static readonly int Mon = 1 << (int)DayOfWeek.Monday;
    private static readonly int Fri = 1 << (int)DayOfWeek.Friday;
    // 2024-01-01 is a Monday.
    private static DateTime At(int day, int h, int m = 0) => new(2024, 1, day, h, m, 0);

    [Fact]
    public void daytime_window_matches_inside_only()
    {
        Assert.True(ShippingQuoteMath.IsInWindow(Mon, TimeSpan.FromHours(18), TimeSpan.FromHours(21), At(1, 19)));
        Assert.False(ShippingQuoteMath.IsInWindow(Mon, TimeSpan.FromHours(18), TimeSpan.FromHours(21), At(1, 21)));
        Assert.False(ShippingQuoteMath.IsInWindow(Mon, TimeSpan.FromHours(18), TimeSpan.FromHours(21), At(2, 19)));
    }

    [Fact]
    public void overnight_window_spills_into_next_day()
    {
        var s = TimeSpan.FromHours(22); var e = TimeSpan.FromHours(2);
        Assert.True(ShippingQuoteMath.IsInWindow(Fri, s, e, At(5, 23)));       // Friday 23:00
        Assert.True(ShippingQuoteMath.IsInWindow(Fri, s, e, At(6, 1)));        // Saturday 01:00 (started Friday)
        Assert.False(ShippingQuoteMath.IsInWindow(Fri, s, e, At(5, 1)));       // Friday 01:00 belongs to Thursday's window
        Assert.False(ShippingQuoteMath.IsInWindow(Fri, s, e, At(6, 12)));
    }

    [Fact]
    public void empty_window_or_no_days_never_matches()
    {
        Assert.False(ShippingQuoteMath.IsInWindow(Mon, TimeSpan.FromHours(5), TimeSpan.FromHours(5), At(1, 5)));
        Assert.False(ShippingQuoteMath.IsInWindow(0, TimeSpan.FromHours(1), TimeSpan.FromHours(23), At(1, 5)));
    }

    [Fact]
    public void day_mask_round_trips()
    {
        var mask = ShippingQuoteMath.DaysToMask(new[] { 0, 6, 3, 9 });
        Assert.Equal(new[] { 0, 3, 6 }, ShippingQuoteMath.MaskToDays(mask));
    }

    [Fact]
    public void quote_adds_surcharge_and_free_shipping_waives_everything()
    {
        var peak = ShippingQuoteMath.Surcharge(60m, 10m, 10m);   // 10 + 6
        Assert.Equal(16m, peak);
        Assert.Equal((60m, 16m, false, 76m), ShippingQuoteMath.Compute(60m, 1000m, 500m, peak));
        Assert.Equal((0m, 0m, true, 0m), ShippingQuoteMath.Compute(60m, 1000m, 1000m, peak));
        Assert.Equal((60m, 0m, false, 60m), ShippingQuoteMath.Compute(60m, null, 99999m, 0m));
    }

    [Fact]
    public void city_match_is_case_insensitive_and_trimmed()
    {
        Assert.True(ShippingQuoteMath.CityMatches("Dhaka, Gazipur", " dhaka "));
        Assert.False(ShippingQuoteMath.CityMatches("Dhaka", "Dhaka City"));
        Assert.False(ShippingQuoteMath.CityMatches("Dhaka", null));
    }

    [Fact]
    public void sla_status_transitions()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0);
        Assert.Equal(SlaStatus.OnTrack, SlaMath.Evaluate(start, null, null, start.AddHours(10), 48, 168).Ship);
        Assert.Equal(SlaStatus.AtRisk, SlaMath.Evaluate(start, null, null, start.AddHours(40), 48, 168).Ship);
        Assert.Equal(SlaStatus.Breached, SlaMath.Evaluate(start, null, null, start.AddHours(49), 48, 168).Ship);
        Assert.Equal(SlaStatus.OnTrack, SlaMath.Evaluate(start, start.AddHours(47), null, start.AddHours(100), 48, 168).Ship);
        Assert.Equal(SlaStatus.Breached, SlaMath.Evaluate(start, start.AddHours(60), null, start.AddHours(61), 48, 168).Ship);
        var r = SlaMath.Evaluate(start, start.AddHours(1), null, start.AddHours(200), 48, 168);
        Assert.Equal(SlaStatus.Breached, r.Deliver);
        Assert.Equal(SlaStatus.Breached, r.Overall);
    }

    [Fact]
    public void partial_refund_last_units_take_the_remainder()
    {
        // net 10.00 over 3 units: 1 unit = 3.33, remaining 2 units = 6.67 -> sums to 10.00
        var first = PartialRefundMath.LineRefund(10m, 3, 0, 0m, 1);
        var rest = PartialRefundMath.LineRefund(10m, 3, 1, first, 2);
        Assert.Equal(3.33m, first);
        Assert.Equal(10m, first + rest);
        Assert.Throws<ArgumentException>(() => PartialRefundMath.LineRefund(10m, 3, 0, 0m, 4));
    }

    [Fact]
    public void store_split_sums_back_to_the_order()
    {
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            new() { StoreId = s1, Quantity = 1, UnitPrice = 100m, Total = 100m, CommissionAmount = 10m },
            new() { StoreId = s2, Quantity = 2, UnitPrice = 50m, Total = 100m, CommissionAmount = 5m },
            new() { StoreId = s1, Quantity = 1, UnitPrice = 33m, Total = 33m, CommissionAmount = 3m },
        };
        var order = new Order { DiscountAmount = 10m, ShippingFee = 50m, Tax = 7m, Total = 233m - 10m + 50m + 7m };
        var split = PartialRefundService.Split(order, items, new Dictionary<Guid, decimal>(), new Dictionary<Guid, string>());
        Assert.Equal(2, split.Count);
        Assert.Equal(order.Subtotal == 0m ? 233m : order.Subtotal, split.Sum(x => x.GrossSubtotal));
        Assert.Equal(10m, split.Sum(x => x.Discount));
        Assert.Equal(50m, split.Sum(x => x.Shipping));
        Assert.Equal(7m, split.Sum(x => x.Tax));
        Assert.Equal(order.Total, split.Sum(x => x.Total));
    }

    [Fact]
    public void rate_math_is_safe_and_clamped()
    {
        Assert.Equal(0m, MetricsMath.Rate(5, 0));
        Assert.Equal(0.25m, MetricsMath.Rate(1, 4));
        Assert.Equal(1m, MetricsMath.Rate(9, 3));
        Assert.Equal(0.3333m, MetricsMath.Rate(1, 3));
    }

    [Fact]
    public void pickup_window_and_transitions()
    {
        var now = new DateTime(2024, 1, 1, 10, 0, 0);
        Assert.Null(ReturnPickupService.ValidateWindow(now.AddHours(2), now.AddHours(4), now));
        Assert.NotNull(ReturnPickupService.ValidateWindow(now.AddHours(-1), now.AddHours(1), now));
        Assert.NotNull(ReturnPickupService.ValidateWindow(now.AddHours(1), now.AddHours(20), now));
        Assert.True(ReturnPickupMath.CanTransition(ReturnPickupStatus.Scheduled, ReturnPickupStatus.PickedUp));
        Assert.False(ReturnPickupMath.CanTransition(ReturnPickupStatus.Requested, ReturnPickupStatus.PickedUp));
        Assert.False(ReturnPickupMath.CanTransition(ReturnPickupStatus.ReceivedByStore, ReturnPickupStatus.Failed));
    }
}
