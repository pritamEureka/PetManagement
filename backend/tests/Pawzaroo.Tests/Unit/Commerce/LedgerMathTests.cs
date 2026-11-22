using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.Commerce;

public class LedgerMathTests
{
    [Fact]
    public void allocation_sums_exactly_to_the_discount()
    {
        var totals = new[] { 10.00m, 10.00m, 10.00m };
        var alloc = LedgerMath.AllocateProRata(totals, 1.00m);
        Assert.Equal(1.00m, alloc.Sum());
        Assert.All(alloc, a => Assert.InRange(a, 0.33m, 0.34m));
    }

    [Fact]
    public void allocation_is_proportional_to_line_totals()
    {
        var alloc = LedgerMath.AllocateProRata(new[] { 75m, 25m }, 20m);
        Assert.Equal(new[] { 15m, 5m }, alloc);
    }

    [Fact]
    public void leftover_cent_goes_to_largest_remainder_then_earliest_line()
    {
        // 0.01 over three equal lines: exactly one cent, earliest line wins the tie.
        var alloc = LedgerMath.AllocateProRata(new[] { 5m, 5m, 5m }, 0.01m);
        Assert.Equal(new[] { 0.01m, 0m, 0m }, alloc);
    }

    [Fact]
    public void discount_is_capped_at_the_sum_of_lines()
    {
        var alloc = LedgerMath.AllocateProRata(new[] { 3m, 2m }, 100m);
        Assert.Equal(new[] { 3m, 2m }, alloc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void non_positive_discount_allocates_nothing(double discount)
    {
        Assert.All(LedgerMath.AllocateProRata(new[] { 10m, 20m }, (decimal)discount), a => Assert.Equal(0m, a));
    }

    [Fact]
    public void empty_or_zero_lines_are_safe()
    {
        Assert.Empty(LedgerMath.AllocateProRata(Array.Empty<decimal>(), 5m));
        Assert.All(LedgerMath.AllocateProRata(new[] { 0m, 0m }, 5m), a => Assert.Equal(0m, a));
    }

    [Fact]
    public void net_of_discount_reduces_each_line_by_its_share()
    {
        var net = LedgerMath.NetOfDiscount(new[] { 60m, 40m }, 10m);
        Assert.Equal(new[] { 54m, 36m }, net);
        Assert.Equal(90m, net.Sum());
    }

    [Fact]
    public void net_never_goes_negative()
    {
        var net = LedgerMath.NetOfDiscount(new[] { 1m, 1m }, 50m);
        Assert.All(net, n => Assert.True(n >= 0m));
    }

    [Theory]
    [InlineData(null, 100, 1.0)]
    [InlineData(50.0, 100, 0.5)]
    [InlineData(250.0, 100, 1.0)]
    [InlineData(-3.0, 100, 0.0)]
    [InlineData(10.0, 0, 0.0)]
    public void refund_ratio_is_clamped(double? amount, double total, double expected)
    {
        Assert.Equal((decimal)expected, LedgerMath.RefundRatio(amount.HasValue ? (decimal)amount.Value : null, (decimal)total));
    }

    [Fact]
    public void commission_reversal_is_proportional_to_the_refunded_share()
    {
        Assert.Equal(5.00m, LedgerMath.CommissionReversal(10m, 45m, 90m));
        Assert.Equal(10.00m, LedgerMath.CommissionReversal(10m, 500m, 90m));
        Assert.Equal(0m, LedgerMath.CommissionReversal(0m, 45m, 90m));
        Assert.Equal(0m, LedgerMath.CommissionReversal(10m, 0m, 90m));
    }
}
