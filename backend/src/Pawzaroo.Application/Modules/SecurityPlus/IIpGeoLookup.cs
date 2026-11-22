namespace Pawzaroo.Application.Modules.SecurityPlus;

/// <summary>Best-known location of a client IP. Any field may be null.</summary>
public record IpGeo(string? Country, string? City, double? Lat, double? Lon)
{
    public bool IsEmpty => Country is null && City is null && Lat is null && Lon is null;
}

/// <summary>
/// Pluggable IP geolocation used by login anomaly detection. The default implementation knows nothing
/// (<c>NoOpIpGeoLookup</c>); swap in a GeoIP database or an edge-proxy header reader (<c>HeaderIpGeoLookup</c>).
/// </summary>
public interface IIpGeoLookup
{
    /// <summary>Location for <paramref name="ip"/> (the current request's client), or null when unknown.</summary>
    IpGeo? Lookup(string? ip);
}

public sealed class NoOpIpGeoLookup : IIpGeoLookup
{
    public IpGeo? Lookup(string? ip) => null;
}
