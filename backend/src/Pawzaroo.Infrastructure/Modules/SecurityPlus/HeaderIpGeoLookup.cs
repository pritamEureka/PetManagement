using System.Globalization;
using Microsoft.AspNetCore.Http;
using Pawzaroo.Application.Modules.SecurityPlus;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

/// <summary>
/// Reads geolocation that an EDGE PROXY already resolved and forwarded as request headers:
/// <c>CF-IPCountry</c> (Cloudflare) or <c>CloudFront-Viewer-Country</c> (CloudFront) for the country, and
/// <c>X-Geo-Lat</c>/<c>X-Geo-Lon</c> (+ optional <c>X-Geo-City</c>/<c>CloudFront-Viewer-City</c>) from a custom edge rule.
/// SECURITY: headers are client-controlled unless the edge overwrites/strips them; only enable this behind such a proxy
/// (<c>Security:Geo:Provider=headers</c>). Without it, no geo data exists and the speed rule can never run.
/// </summary>
public sealed class HeaderIpGeoLookup : IIpGeoLookup
{
    private readonly IHttpContextAccessor _http;
    public HeaderIpGeoLookup(IHttpContextAccessor http) => _http = http;

    public IpGeo? Lookup(string? ip)
    {
        var req = _http.HttpContext?.Request;
        if (req is null) return null;
        return Parse(name => req.Headers.TryGetValue(name, out var v) ? v.ToString() : null);
    }

    /// <summary>Pure parser over a header accessor (unit-testable). Returns null when nothing usable is present.</summary>
    public static IpGeo? Parse(Func<string, string?> header)
    {
        var country = NormalizeCountry(header("CF-IPCountry")) ?? NormalizeCountry(header("CloudFront-Viewer-Country"));
        var city = Clean(header("X-Geo-City") ?? header("CloudFront-Viewer-City"), 128);
        var lat = ParseCoord(header("X-Geo-Lat"), 90);
        var lon = ParseCoord(header("X-Geo-Lon"), 180);
        if (lat is null || lon is null) { lat = null; lon = null; }   // only a complete pair is meaningful

        var geo = new IpGeo(country, city, lat, lon);
        return geo.IsEmpty ? null : geo;
    }

    private static string? NormalizeCountry(string? v)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v) || v.Length != 2 || !v.All(char.IsAsciiLetter)) return null;
        v = v.ToUpperInvariant();
        return v is "XX" or "T1" ? null : v;   // Cloudflare: unknown / Tor
    }

    private static double? ParseCoord(string? v, double limit)
        => double.TryParse(v?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
           && !double.IsNaN(d) && Math.Abs(d) <= limit ? d : null;

    private static string? Clean(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}
