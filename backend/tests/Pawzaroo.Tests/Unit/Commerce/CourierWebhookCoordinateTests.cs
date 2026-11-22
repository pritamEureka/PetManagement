using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.Commerce;

public class CourierWebhookCoordinateTests
{
    [Theory]
    [InlineData(23.8, 90.4)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    public void valid_coordinates_are_kept(double lat, double lng)
    {
        var (a, b) = CourierWebhookService.SanitizeCoordinates(lat, lng);
        Assert.Equal(lat, a);
        Assert.Equal(lng, b);
    }

    [Theory]
    [InlineData(91, 10)]
    [InlineData(10, 181)]
    [InlineData(-90.1, 0)]
    [InlineData(double.NaN, 0)]
    public void invalid_coordinates_are_dropped_not_rejected(double lat, double lng)
    {
        var (a, b) = CourierWebhookService.SanitizeCoordinates(lat, lng);
        Assert.Null(a);
        Assert.Null(b);
    }

    [Fact]
    public void missing_coordinates_stay_missing()
    {
        var (a, b) = CourierWebhookService.SanitizeCoordinates(null, null);
        Assert.Null(a);
        Assert.Null(b);
    }
}
