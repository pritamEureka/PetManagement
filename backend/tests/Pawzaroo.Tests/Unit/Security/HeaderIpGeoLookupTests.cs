using FluentAssertions;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Infrastructure.Modules.SecurityPlus;
using Xunit;

namespace Pawzaroo.Tests.Unit.Security;

public class HeaderIpGeoLookupTests
{
    private static IpGeo? Parse(params (string Name, string Value)[] headers)
    {
        var map = headers.ToDictionary(h => h.Name, h => h.Value, StringComparer.OrdinalIgnoreCase);
        return HeaderIpGeoLookup.Parse(n => map.TryGetValue(n, out var v) ? v : null);
    }

    [Fact]
    public void No_headers_yields_null()
        => Parse().Should().BeNull();

    [Fact]
    public void Cloudflare_country_is_upper_cased()
        => Parse(("CF-IPCountry", "bd")).Should().Be(new IpGeo("BD", null, null, null));

    [Fact]
    public void Cloudfront_country_is_used_when_cloudflare_missing()
        => Parse(("CloudFront-Viewer-Country", "DE"))!.Country.Should().Be("DE");

    [Theory]
    [InlineData("XX")]
    [InlineData("T1")]
    [InlineData("USA")]
    [InlineData("1")]
    [InlineData("")]
    public void Unknown_or_malformed_country_is_ignored(string v)
        => Parse(("CF-IPCountry", v)).Should().BeNull();

    [Fact]
    public void Lat_lon_pair_parses_with_invariant_culture()
    {
        var g = Parse(("X-Geo-Lat", "23.8103"), ("X-Geo-Lon", "90.4125"), ("CF-IPCountry", "BD"));
        g!.Lat.Should().BeApproximately(23.8103, 1e-9);
        g.Lon.Should().BeApproximately(90.4125, 1e-9);
    }

    [Fact]
    public void Lone_latitude_is_dropped()
        => Parse(("X-Geo-Lat", "23.8")).Should().BeNull();

    [Theory]
    [InlineData("91", "10")]
    [InlineData("10", "181")]
    [InlineData("abc", "10")]
    [InlineData("NaN", "10")]
    public void Out_of_range_or_invalid_coordinates_are_dropped(string lat, string lon)
        => Parse(("X-Geo-Lat", lat), ("X-Geo-Lon", lon)).Should().BeNull();

    [Fact]
    public void Speed_rule_runs_only_when_both_sides_have_coordinates()
    {
        var t0 = DateTime.UtcNow;
        var dhaka = new LoginGeo("1.1.1.1", null, "BD", 23.81, 90.41, t0);
        var london = new LoginGeo("2.2.2.2", null, "GB", 51.50, -0.12, t0.AddMinutes(30));
        SignalRules.EvaluateTravel(dhaka, london)!.Should().Contain("km/h");

        // Without coordinates and outside the 1h window only the country rule exists and it does not fire.
        var a = dhaka with { Lat = null, Lon = null };
        var b = london with { Lat = null, Lon = null, At = t0.AddHours(5) };
        SignalRules.EvaluateTravel(a, b).Should().BeNull();
    }
}
