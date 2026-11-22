using FluentAssertions;
using Pawzaroo.Application.Modules.Intelligence;
using Xunit;

namespace Pawzaroo.Tests.Unit.Intelligence;

public class StatsTests
{
    [Fact]
    public void MovingAverage_uses_trailing_window_and_partial_start()
    {
        var ma = Stats.MovingAverage(new double[] { 2, 4, 6, 8 }, 2);
        ma.Should().Equal(2, 3, 5, 7);
    }

    [Fact]
    public void MovingAverage_rejects_bad_window()
    {
        var act = () => Stats.MovingAverage(new double[] { 1 }, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LinearRegression_recovers_exact_line()
    {
        var (slope, intercept) = Stats.LinearRegression(new double[] { 1, 3, 5, 7 });
        slope.Should().BeApproximately(2, 1e-9);
        intercept.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void LinearRegression_handles_empty_and_single()
    {
        Stats.LinearRegression(Array.Empty<double>()).Should().Be((0d, 0d));
        Stats.LinearRegression(new double[] { 5 }).Should().Be((0d, 5d));
    }

    [Fact]
    public void ProjectLinear_extends_trend_and_clamps_at_zero()
    {
        Stats.ProjectLinear(new double[] { 1, 2, 3 }, 2).Should().Equal(4, 5);
        Stats.ProjectLinear(new double[] { 3, 2, 1 }, 4).Should().Equal(0, 0, 0, 0);
    }

    [Fact]
    public void ZScores_flag_outlier_and_zero_for_constant_series()
    {
        var z = Stats.ZScores(new double[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 50 });
        z[^1].Should().BeGreaterThan(2.5);
        Stats.ZScores(new double[] { 4, 4, 4 }).Should().Equal(0, 0, 0);
    }

    [Fact]
    public void StdDev_and_Mean_are_population_stats()
    {
        Stats.Mean(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }).Should().Be(5);
        Stats.StdDev(new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }).Should().BeApproximately(2, 1e-9);
    }

    [Fact]
    public void Rate_is_percentage_and_safe_on_zero()
    {
        Stats.Rate(1, 4).Should().Be(25);
        Stats.Rate(1, 0).Should().Be(0);
    }
}
