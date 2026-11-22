using System.Security.Cryptography;
using System.Text;
using Pawzaroo.Infrastructure.Modules.Adoption;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>Pure deposit / cancellation maths. No I/O so it is trivially unit-testable.</summary>
public static class BookingPolicyMath
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Deposit due at booking. A fixed amount wins over a percentage; never above the fee.</summary>
    public static decimal DepositDue(decimal fee, decimal? percent, decimal? fixedAmount)
    {
        if (fee <= 0) return 0;
        decimal d = 0;
        if (fixedAmount is > 0) d = fixedAmount.Value;
        else if (percent is > 0) d = fee * Math.Min(percent.Value, 100m) / 100m;
        return Round(Math.Min(d, fee));
    }

    public sealed record CancellationResult(bool IsLate, decimal Fee, decimal Forfeited, decimal Refund, decimal Outstanding);

    /// <summary>
    /// Late = cancelled less than <paramref name="windowHours"/> before the visit. The fee is a percentage of the
    /// full appointment amount; it is taken from the deposit paid first (forfeited), the remainder of the deposit
    /// is refunded and any shortfall is reported as outstanding. Doctor-initiated cancellations never carry a fee.
    /// </summary>
    public static CancellationResult Cancellation(DateTime scheduledAtUtc, DateTime nowUtc, int windowHours,
        decimal feePercent, decimal appointmentAmount, decimal depositPaid, bool byDoctor)
    {
        depositPaid = Math.Max(0, depositPaid);
        var hoursLeft = (scheduledAtUtc - nowUtc).TotalHours;
        var late = !byDoctor && hoursLeft < Math.Max(0, windowHours);
        if (!late) return new CancellationResult(false, 0, 0, Round(depositPaid), 0);
        var fee = Round(Math.Max(0, appointmentAmount) * Math.Clamp(feePercent, 0, 100) / 100m);
        var forfeited = Math.Min(fee, depositPaid);
        return new CancellationResult(true, fee, Round(forfeited), Round(depositPaid - forfeited), Round(fee - forfeited));
    }

    public static CancellationResult NoShow(decimal feePercent, decimal appointmentAmount, decimal depositPaid)
    {
        depositPaid = Math.Max(0, depositPaid);
        var fee = Round(Math.Max(0, appointmentAmount) * Math.Clamp(feePercent, 0, 100) / 100m);
        var forfeited = Math.Min(fee, depositPaid);
        return new CancellationResult(true, fee, Round(forfeited), Round(depositPaid - forfeited), Round(fee - forfeited));
    }
}

/// <summary>Deterministic SHA-256 over canonical clinical content + doctor + timestamp.</summary>
public static class EncounterSignature
{
    public static string Canonical(string? chiefComplaint, string? findings, string? diagnosis, string? treatmentPlan,
        IEnumerable<(string Code, string System)> codes, Guid doctorId, DateTime atUtc, int version)
    {
        static string N(string? s) => (s ?? "").Trim().Replace("\r\n", "\n");
        var sb = new StringBuilder();
        sb.Append("v1\n");
        sb.Append("cc:").Append(N(chiefComplaint)).Append('\n');
        sb.Append("fi:").Append(N(findings)).Append('\n');
        sb.Append("dx:").Append(N(diagnosis)).Append('\n');
        sb.Append("tp:").Append(N(treatmentPlan)).Append('\n');
        foreach (var c in codes.Select(c => (System: c.System.Trim().ToUpperInvariant(), Code: c.Code.Trim().ToUpperInvariant()))
                     .OrderBy(c => c.System, StringComparer.Ordinal).ThenBy(c => c.Code, StringComparer.Ordinal))
            sb.Append("code:").Append(c.System).Append(':').Append(c.Code).Append('\n');
        sb.Append("doctor:").Append(doctorId.ToString("D")).Append('\n');
        sb.Append("at:").Append(atUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("ver:").Append(version);
        return sb.ToString();
    }

    public static string Hash(string canonical) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}

/// <summary>Patient-return rate: share of patients with at least two visits within a rolling window of days.</summary>
public static class ReturnRateMath
{
    public sealed record Result(int Patients, int Returning, double Rate);

    /// <summary>
    /// A patient "returns" when some pair of consecutive visits is no more than <paramref name="withinDays"/> apart
    /// (equivalently, at least 2 visits fall inside a window of that length).
    /// </summary>
    public static Result Compute(IEnumerable<(Guid PatientId, DateTime VisitUtc)> visits, int withinDays)
    {
        var groups = visits.GroupBy(v => v.PatientId).ToList();
        var returning = 0;
        foreach (var g in groups)
        {
            var ordered = g.Select(v => v.VisitUtc).OrderBy(d => d).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if ((ordered[i] - ordered[i - 1]).TotalDays <= withinDays) { returning++; break; }
        }
        var n = groups.Count;
        return new Result(n, returning, n == 0 ? 0 : Math.Round((double)returning / n, 4));
    }
}

public sealed record EmergencyCandidate(Guid DoctorId, bool Is24x7, bool AcceptsWalkIn, string? City,
    double? Latitude, double? Longitude, bool HasPhone);

/// <summary>Ordering for emergency discovery: 24/7 first, then nearest, then same-city, then the rest.</summary>
public static class EmergencyOrdering
{
    public sealed record Ranked(EmergencyCandidate Candidate, double? DistanceKm, bool CityMatch);

    public static List<Ranked> Order(IEnumerable<EmergencyCandidate> candidates, double? lat, double? lng, string? city)
    {
        var haveOrigin = GeoSearch.IsValidPoint(lat, lng) && lat.HasValue && lng.HasValue;
        var cityNorm = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        var ranked = candidates.Select(c =>
        {
            double? dist = haveOrigin && c.Latitude is { } la && c.Longitude is { } lo && GeoSearch.IsValidPoint(la, lo)
                ? Math.Round(GeoSearch.DistanceKm(lat!.Value, lng!.Value, la, lo), 1) : null;
            var cityMatch = cityNorm is not null && string.Equals(c.City?.Trim(), cityNorm, StringComparison.OrdinalIgnoreCase);
            return new Ranked(c, dist, cityMatch);
        });
        return ranked
            .OrderByDescending(r => r.Candidate.Is24x7)
            .ThenBy(r => r.DistanceKm.HasValue ? 0 : 1)        // coordinates beat no coordinates
            .ThenBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenByDescending(r => r.CityMatch)
            .ThenByDescending(r => r.Candidate.AcceptsWalkIn)
            .ThenBy(r => r.Candidate.DoctorId)
            .ToList();
    }
}
