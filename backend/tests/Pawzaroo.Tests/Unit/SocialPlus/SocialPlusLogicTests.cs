using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Modules.Communities;
using Pawzaroo.Infrastructure.Modules.Pets;
using Xunit;

namespace Pawzaroo.Tests.Unit.SocialPlus;

public class LostFoundLogicTests
{
    private static MatchProfile P(string? chip = null, string? breed = "Labrador", string? color = "Black", string? markings = null,
        double? lat = 23.80, double? lng = 90.40, string city = "Dhaka", DateTime? seen = null) =>
        new(AnimalType.Dog, breed, color, markings, chip, city, null, lat, lng, seen ?? new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void different_species_never_match()
    {
        var a = P();
        var b = a with { AnimalType = AnimalType.Cat };
        Assert.Equal(0, LostFoundLogic.Score(a, b).Score);
    }

    [Fact]
    public void microchip_match_dominates()
    {
        var r = LostFoundLogic.Score(P(chip: "985112345678901", breed: null, color: null),
            P(chip: "985 112 345 678 901", breed: "Beagle", color: "White", lat: null, lng: null, city: "Other"));
        Assert.True(r.MicrochipMatch);
        Assert.True(r.Score >= 95);
        Assert.Contains(r.Reasons, x => x.Contains("Microchip"));
    }

    [Fact]
    public void closer_is_better_and_distance_decays_to_zero()
    {
        var near = LostFoundLogic.Score(P(), P(lat: 23.801, lng: 90.401)).Score;
        var far = LostFoundLogic.Score(P(), P(lat: 23.95, lng: 90.40)).Score;
        var veryFar = LostFoundLogic.Score(P(), P(lat: 25.5, lng: 90.40)).Score;
        Assert.True(near > far);
        Assert.True(far > veryFar);
        Assert.Equal(0, LostFoundLogic.DistancePoints(30));
        Assert.Equal(LostFoundLogic.MaxDistancePoints, LostFoundLogic.DistancePoints(0));
    }

    [Fact]
    public void falls_back_to_city_when_coordinates_are_missing()
    {
        var r = LostFoundLogic.Score(P(lat: null, lng: null), P(lat: null, lng: null));
        Assert.Contains(r.Reasons, x => x.StartsWith("Same city"));
    }

    [Fact]
    public void markings_overlap_adds_points()
    {
        var with = LostFoundLogic.Score(P(markings: "white patch on chest"), P(markings: "White patch, left ear"));
        var without = LostFoundLogic.Score(P(), P());
        Assert.Contains(with.Reasons, x => x.Contains("markings"));
        Assert.True(with.Score > without.Score);
    }

    [Fact]
    public void score_is_capped_at_100()
    {
        var r = LostFoundLogic.Score(P(chip: "123456789012345", markings: "white patch"), P(chip: "123456789012345", markings: "white patch"));
        Assert.Equal(100, r.Score);
    }

    [Theory]
    [InlineData(23.8103, 90.4125)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(0.0, 0.0)]
    public void fuzz_is_deterministic_and_bounded(double lat, double lng)
    {
        var id = Guid.NewGuid();
        var a = LostFoundLogic.Fuzz(id, lat, lng);
        var b = LostFoundLogic.Fuzz(id, lat, lng);
        Assert.Equal(a, b);
        var km = GeoSearch.DistanceKm(lat, lng, a.Lat!.Value, a.Lng!.Value);
        Assert.InRange(km, 0, 0.6);
    }

    [Fact]
    public void fuzz_differs_per_report_and_drops_invalid_points()
    {
        var x = LostFoundLogic.Fuzz(Guid.NewGuid(), 23.8, 90.4);
        var y = LostFoundLogic.Fuzz(Guid.NewGuid(), 23.8, 90.4);
        Assert.NotEqual(x, y);
        Assert.Null(LostFoundLogic.Fuzz(Guid.NewGuid(), 123, 0).Lat);
        Assert.Null(LostFoundLogic.Fuzz(Guid.NewGuid(), null, null).Lng);
    }

    [Fact]
    public void expiry_and_fence_checks()
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(LostFoundLogic.IsExpired(now.AddDays(-14), 14, now));
        Assert.False(LostFoundLogic.IsExpired(now.AddDays(-13), 14, now));
        Assert.False(LostFoundLogic.IsExpired(now.AddDays(-400), 0, now));
        Assert.True(LostFoundLogic.FenceCovers(23.80, 90.40, 5, 23.81, 90.41));
        Assert.False(LostFoundLogic.FenceCovers(23.80, 90.40, 1, 24.5, 90.41));
        Assert.False(LostFoundLogic.FenceCovers(23.80, 90.40, 5, null, null));
    }
}

public class ReputationAndPollTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(149, 2)]
    [InlineData(150, 3)]
    [InlineData(5000, 5)]
    public void levels_follow_thresholds(int total, int level) => Assert.Equal(level, ReputationRules.LevelFor(total));

    [Fact]
    public void caps_limit_positive_points_only()
    {
        Assert.True(ReputationRules.WithinCaps(ReputationKind.PostLiked, 1, 0, 0));
        Assert.False(ReputationRules.WithinCaps(ReputationKind.PostLiked, 1, 0, 20));
        Assert.False(ReputationRules.WithinCaps(ReputationKind.EventHosted, 10, ReputationRules.DailyPositiveCap - 5, 0));
        Assert.True(ReputationRules.WithinCaps(ReputationKind.Penalty, -10, ReputationRules.DailyPositiveCap, 0));
        Assert.True(ReputationRules.PointsFor(ReputationKind.Penalty) < 0);
    }

    [Fact]
    public void source_key_is_stable_and_order_sensitive()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Equal(ReputationRules.SourceKey(a, b), ReputationRules.SourceKey(a, b));
        Assert.NotEqual(ReputationRules.SourceKey(a, b), ReputationRules.SourceKey(b, a));
    }

    [Fact]
    public void poll_percentages_are_of_voters()
    {
        var o1 = Guid.NewGuid(); var o2 = Guid.NewGuid();
        var p = PollTally.Percentages(new[] { new TallyOption(o1, 3), new TallyOption(o2, 1) }, 4);
        Assert.Equal(75.0, p[o1]);
        Assert.Equal(25.0, p[o2]);
        Assert.Equal(0, PollTally.Percentages(new[] { new TallyOption(o1, 0) }, 0)[o1]);
    }

    [Fact]
    public void ballot_validation_enforces_single_choice()
    {
        var o1 = Guid.NewGuid(); var o2 = Guid.NewGuid();
        var ids = new HashSet<Guid> { o1, o2 };
        Assert.Null(PollTally.ValidateBallot(ids, new[] { o1 }, false));
        Assert.NotNull(PollTally.ValidateBallot(ids, new[] { o1, o2 }, false));
        Assert.Null(PollTally.ValidateBallot(ids, new[] { o1, o2 }, true));
        Assert.NotNull(PollTally.ValidateBallot(ids, new[] { o1, o1 }, true));
        Assert.NotNull(PollTally.ValidateBallot(ids, new[] { Guid.NewGuid() }, true));
        Assert.NotNull(PollTally.ValidateBallot(ids, Array.Empty<Guid>(), true));
    }
}
