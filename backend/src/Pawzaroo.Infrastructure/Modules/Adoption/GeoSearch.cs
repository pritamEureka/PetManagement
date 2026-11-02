namespace Pawzaroo.Infrastructure.Modules.Adoption;

/// <summary>
/// Plain-Postgres geo helpers (no PostGIS). Distances use the haversine
/// formula, which Npgsql translates to SQL trig functions; radius queries
/// add a lat/lng bounding box first so the (status, latitude, longitude)
/// index can narrow the scan before the exact distance check.
/// </summary>
internal static class GeoSearch
{
    public const double EarthRadiusKm = 6371.0;
    public const double KmPerDegreeLat = 111.32;

    /// <summary>
    /// Rounds a listing's point to 2 decimals (~1.1 km) so a pet's home address
    /// can't be recovered from search results. Both coordinates or neither.
    /// </summary>
    public static (double? Lat, double? Lng) Approximate(double? lat, double? lng)
    {
        if (lat is not { } a || lng is not { } o) return (null, null);
        if (a is < -90 or > 90 || o is < -180 or > 180 || double.IsNaN(a) || double.IsNaN(o)) return (null, null);
        return (Math.Round(a, 2), Math.Round(o, 2));
    }

    public static bool IsValidPoint(double? lat, double? lng) =>
        lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    /// <summary>Degree deltas of a box that fully contains a circle of <paramref name="radiusKm"/>.</summary>
    public static (double DLat, double DLng) BoundingBox(double lat, double radiusKm)
    {
        var dLat = radiusKm / KmPerDegreeLat;
        // Longitude degrees shrink towards the poles; clamp so the box stays finite near them.
        var cos = Math.Max(Math.Cos(lat * Math.PI / 180), 0.01);
        var dLng = Math.Min(radiusKm / (KmPerDegreeLat * cos), 180);
        return (dLat, dLng);
    }

    /// <summary>In-memory haversine — same formula as the SQL expression, used for tests and display.</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        double R(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin((R(lat2) - R(lat1)) / 2), 2)
              + Math.Cos(R(lat1)) * Math.Cos(R(lat2)) * Math.Pow(Math.Sin((R(lng2) - R(lng1)) / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(Math.Min(1.0, a)));
    }
}
