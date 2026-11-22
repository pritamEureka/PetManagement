namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Pure ledger arithmetic (no I/O) so it can be unit tested. All results are 2dp, away-from-zero.
/// </summary>
public static class LedgerMath
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Splits an order-level <paramref name="amount"/> (e.g. a coupon discount) across lines in proportion to
    /// their <paramref name="lineTotals"/> using the largest-remainder method on cents:
    ///   share_i = amount * total_i / sum(totals)         (exact)
    ///   floor each share to 0.01, then hand the leftover cents one by one to the lines with the largest
    ///   fractional remainder (ties go to the earlier line).
    /// Guarantees sum(result) == Round(amount) (capped at sum(totals)) and 0 &lt;= result_i &lt;= total_i.
    /// </summary>
    public static decimal[] AllocateProRata(IReadOnlyList<decimal> lineTotals, decimal amount)
    {
        var result = new decimal[lineTotals.Count];
        var sum = lineTotals.Sum(t => Math.Max(t, 0m));
        if (result.Length == 0 || amount <= 0m || sum <= 0m) return result;

        var totalCents = (long)(Round(Math.Min(amount, sum)) * 100m);
        var exact = new decimal[result.Length];
        var cents = new long[result.Length];
        long assigned = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var w = Math.Max(lineTotals[i], 0m);
            exact[i] = totalCents * w / sum;           // in cents, fractional
            cents[i] = (long)Math.Floor(exact[i]);
            assigned += cents[i];
        }

        var leftover = totalCents - assigned;
        foreach (var i in Enumerable.Range(0, result.Length)
                     .OrderByDescending(i => exact[i] - cents[i]).ThenBy(i => i))
        {
            if (leftover <= 0) break;
            if (cents[i] + 1 > (long)(Math.Max(lineTotals[i], 0m) * 100m)) continue; // never exceed the line
            cents[i]++;
            leftover--;
        }

        for (var i = 0; i < result.Length; i++) result[i] = cents[i] / 100m;
        return result;
    }

    /// <summary>Item totals net of their pro-rata share of the order-level discount.</summary>
    public static decimal[] NetOfDiscount(IReadOnlyList<decimal> lineTotals, decimal discount)
    {
        var alloc = AllocateProRata(lineTotals, discount);
        var net = new decimal[lineTotals.Count];
        for (var i = 0; i < net.Length; i++) net[i] = Round(lineTotals[i] - alloc[i]);
        return net;
    }

    /// <summary>
    /// Refund ratio of an order: null amount = 1 (full); otherwise amount / orderTotal clamped to [0,1].
    /// </summary>
    public static decimal RefundRatio(decimal? amount, decimal orderTotal)
    {
        if (orderTotal <= 0m) return 0m;
        return amount.HasValue ? Math.Min(Math.Max(amount.Value, 0m) / orderTotal, 1m) : 1m;
    }

    /// <summary>Commission reversed alongside a partial refund: commission * refunded / net (capped at commission).</summary>
    public static decimal CommissionReversal(decimal commission, decimal refunded, decimal itemNet)
    {
        if (commission <= 0m || itemNet <= 0m || refunded <= 0m) return 0m;
        return Round(commission * Math.Min(refunded / itemNet, 1m));
    }
}
