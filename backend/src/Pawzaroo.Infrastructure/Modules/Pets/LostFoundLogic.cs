using System.Security.Cryptography;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Modules.Adoption;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>One side of a lost/found comparison. Coordinates are the exact (server-only) ones.</summary>
public record MatchProfile(
    AnimalType AnimalType, string? Breed, string? Color, string? Markings, string? Microchip,
    string? City, string? Area, double? Lat, double? Lng,
    DateTime SeenAt, DateTime? WindowStart = null, DateTime? WindowEnd = null);

public record MatchResult(int Score, IReadOnlyList<string> Reasons, bool MicrochipMatch);

/// <summary>
/// Pure, DB-free logic for lost &amp; found: match scoring, public coordinate fuzzing and geofence checks.
/// PostGIS is NOT required: distances are in-memory/SQL haversine via <see cref="GeoSearch"/>.
/// </summary>
public static class LostFoundLogic
{
    public const int MicrochipWeight = 60;
    public const int SpeciesWeight = 15;
    public const int BreedWeight = 12;
    public const int ColorWeight = 12;
    public const int MarkingsWeight = 12;
    public const int MaxDistancePoints = 20;
    public const double DistanceDecayKm = 25;
    public const int MinScore = 35;

    private static string Norm(string? s) => new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool Similar(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && (a.Contains(b.Trim(), StringComparison.OrdinalIgnoreCase) || b.Contains(a.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Share of the smaller token set that appears in the other (tokens of 3+ letters).</summary>
    public static double TokenOverlap(string? a, string? b)
    {
        static HashSet<string> Tokens(string? s) => (s ?? "")
            .Split(new[] { ' ', ',', ';', '.', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length >= 3).ToHashSet();
        var x = Tokens(a); var y = Tokens(b);
        if (x.Count == 0 || y.Count == 0) return 0;
        return (double)x.Count(y.Contains) / Math.Min(x.Count, y.Count);
    }

    /// <summary>Distance points: full at 0 km, linear decay to 0 at <see cref="DistanceDecayKm"/>.</summary>
    public static int DistancePoints(double km) =>
        (int)Math.Round(MaxDistancePoints * Math.Max(0, 1 - km / DistanceDecayKm));

    private static string Band(double km) => km <= 1 ? "Within 1 km" : km <= 5 ? "Within 5 km" : km <= 10 ? "Within 10 km" : "Within 25 km";

    public static MatchResult Score(MatchProfile a, MatchProfile b)
    {
        var reasons = new List<string>();
        if (a.AnimalType != b.AnimalType) return new MatchResult(0, reasons, false);
        var score = SpeciesWeight;
        reasons.Add($"Same species ({b.AnimalType})");

        var chip = Norm(a.Microchip);
        var chipMatch = chip.Length >= 9 && chip == Norm(b.Microchip);
        if (chipMatch) { score += MicrochipWeight; reasons.Add("Microchip number matches exactly"); }

        if (Similar(a.Breed, b.Breed)) { score += BreedWeight; reasons.Add($"Breed: {b.Breed}"); }
        if (Similar(a.Color, b.Color)) { score += ColorWeight; reasons.Add($"Colour: {b.Color}"); }
        var mo = TokenOverlap(a.Markings, b.Markings);
        if (mo > 0) { score += (int)Math.Round(MarkingsWeight * Math.Min(1, mo)); reasons.Add("Similar markings"); }

        if (a.Lat is { } la && a.Lng is { } lo && b.Lat is { } lb && b.Lng is { } lob
            && GeoSearch.IsValidPoint(la, lo) && GeoSearch.IsValidPoint(lb, lob))
        {
            var km = GeoSearch.DistanceKm(la, lo, lb, lob);
            var pts = DistancePoints(km);
            if (pts > 0) { score += pts; reasons.Add(Band(km)); }
        }
        else
        {
            if (string.Equals(a.City?.Trim(), b.City?.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.City))
            { score += 10; reasons.Add($"Same city ({b.City})"); }
            if (Similar(a.Area, b.Area)) { score += 5; reasons.Add("Same area"); }
        }

        if (WindowsOverlap(a, b)) { score += 10; reasons.Add("Time windows overlap"); }
        else
        {
            var days = Math.Abs((a.SeenAt - b.SeenAt).TotalDays);
            if (days <= 3) { score += 8; reasons.Add("Seen within 3 days"); }
            else if (days <= 14) score += 4;
        }

        score = Math.Min(score, 100);
        if (chipMatch) score = Math.Max(score, 95);
        return new MatchResult(score, reasons, chipMatch);
    }

    private static bool WindowsOverlap(MatchProfile a, MatchProfile b)
    {
        var (as_, ae) = (a.WindowStart ?? a.SeenAt, a.WindowEnd ?? a.SeenAt);
        var (bs, be) = (b.WindowStart ?? b.SeenAt, b.WindowEnd ?? b.SeenAt);
        var hasWindow = a.WindowStart.HasValue || a.WindowEnd.HasValue || b.WindowStart.HasValue || b.WindowEnd.HasValue;
        return hasWindow && as_ <= be && bs <= ae;
    }

    // ------------------------------------------------------------ privacy

    public const double GridDegrees = 0.005;     // ~550 m of latitude
    private const double JitterDegrees = 0.001;  // +-110 m, deterministic per report

    /// <summary>
    /// Public coordinates: snapped to a 0.005 degree grid plus a deterministic per-report jitter
    /// (SHA-256 of the report id) so repeated reads never average out the noise and different
    /// reports from the same spot differ. Worst-case error is well under 0.6 km.
    /// </summary>
    public static (double? Lat, double? Lng) Fuzz(Guid reportId, double? lat, double? lng)
    {
        if (lat is not { } a || lng is not { } o || !GeoSearch.IsValidPoint(a, o)) return (null, null);
        var h = SHA256.HashData(reportId.ToByteArray());
        var jLat = ((BitConverter.ToUInt16(h, 0) / 65535.0) * 2 - 1) * JitterDegrees;
        var jLng = ((BitConverter.ToUInt16(h, 2) / 65535.0) * 2 - 1) * JitterDegrees;
        var fa = Math.Round(a / GridDegrees) * GridDegrees + jLat;
        var fo = Math.Round(o / GridDegrees) * GridDegrees + jLng;
        return (Math.Round(Math.Clamp(fa, -90, 90), 5), Math.Round(Math.Clamp(fo, -180, 180), 5));
    }

    /// <summary>True when the exact location should no longer be kept/served (older than <paramref name="days"/>).</summary>
    public static bool IsExpired(DateTime createdAtUtc, int days, DateTime nowUtc) =>
        days > 0 && createdAtUtc.AddDays(days) <= nowUtc;

    /// <summary>Geofence coverage using the report's fuzzed public point.</summary>
    public static bool FenceCovers(double fenceLat, double fenceLng, double radiusKm, double? pubLat, double? pubLng) =>
        pubLat is { } la && pubLng is { } lo && GeoSearch.IsValidPoint(la, lo)
        && GeoSearch.DistanceKm(fenceLat, fenceLng, la, lo) <= radiusKm;
}
