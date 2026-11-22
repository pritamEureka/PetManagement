namespace Pawzaroo.Application.Modules.Intelligence;

/// <summary>Pure statistical helpers (no I/O) used by operations intelligence.</summary>
public static class Stats
{
    /// <summary>Trailing moving average. Early points average over the values available so far.</summary>
    public static double[] MovingAverage(IReadOnlyList<double> values, int window)
    {
        if (window < 1) throw new ArgumentOutOfRangeException(nameof(window));
        var result = new double[values.Count];
        double sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= window) sum -= values[i - window];
            result[i] = sum / Math.Min(i + 1, window);
        }
        return result;
    }

    /// <summary>Ordinary least-squares fit of y over x = 0..n-1.</summary>
    public static (double Slope, double Intercept) LinearRegression(IReadOnlyList<double> y)
    {
        var n = y.Count;
        if (n == 0) return (0, 0);
        if (n == 1) return (0, y[0]);
        double sx = 0, sy = 0, sxy = 0, sxx = 0;
        for (var i = 0; i < n; i++)
        {
            sx += i; sy += y[i]; sxy += i * y[i]; sxx += (double)i * i;
        }
        var denom = n * sxx - sx * sx;
        var slope = denom == 0 ? 0 : (n * sxy - sx * sy) / denom;
        var intercept = (sy - slope * sx) / n;
        return (slope, intercept);
    }

    /// <summary>Projects the fitted line <paramref name="horizon"/> steps beyond the series, clamped at zero.</summary>
    public static double[] ProjectLinear(IReadOnlyList<double> y, int horizon)
    {
        var (slope, intercept) = LinearRegression(y);
        var result = new double[Math.Max(0, horizon)];
        for (var k = 0; k < result.Length; k++)
            result[k] = Math.Max(0, intercept + slope * (y.Count + k));
        return result;
    }

    public static double Mean(IReadOnlyList<double> values) => values.Count == 0 ? 0 : values.Sum() / values.Count;

    /// <summary>Population standard deviation.</summary>
    public static double StdDev(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var m = Mean(values);
        return Math.Sqrt(values.Sum(v => (v - m) * (v - m)) / values.Count);
    }

    /// <summary>Z-score of each value against the series; all zeros when the series has no variance.</summary>
    public static double[] ZScores(IReadOnlyList<double> values)
    {
        var m = Mean(values);
        var sd = StdDev(values);
        return values.Select(v => sd < 1e-12 ? 0d : (v - m) / sd).ToArray();
    }

    public static double Rate(long part, long whole) => whole <= 0 ? 0 : Math.Round(part * 100.0 / whole, 2);
}
