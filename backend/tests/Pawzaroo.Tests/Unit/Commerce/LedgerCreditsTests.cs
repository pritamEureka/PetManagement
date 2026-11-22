using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.Commerce;

public class LedgerCreditsTests
{
    [Fact]
    public void no_credits_equals_net_of_discount()
    {
        var totals = new[] { 60m, 40m };
        Assert.Equal(LedgerMath.NetOfDiscount(totals, 10m), LedgerMath.NetOfDiscountAndCredits(totals, 10m, 0m));
    }

    [Fact]
    public void credits_are_spread_pro_rata_over_discounted_nets()
    {
        // nets after 10 discount = 54 / 36; credits 9 -> 5.40 / 3.60
        var net = LedgerMath.NetOfDiscountAndCredits(new[] { 60m, 40m }, 10m, 9m);
        Assert.Equal(new[] { 48.60m, 32.40m }, net);
    }

    [Fact]
    public void credits_never_push_a_line_below_zero_and_sum_is_exact()
    {
        var net = LedgerMath.NetOfDiscountAndCredits(new[] { 10m, 10m, 10m }, 0m, 1m);
        Assert.Equal(29m, net.Sum());
        Assert.All(net, n => Assert.InRange(n, 0m, 10m));
        var all = LedgerMath.NetOfDiscountAndCredits(new[] { 10m, 5m }, 0m, 100m);
        Assert.Equal(new[] { 0m, 0m }, all);
    }
}
