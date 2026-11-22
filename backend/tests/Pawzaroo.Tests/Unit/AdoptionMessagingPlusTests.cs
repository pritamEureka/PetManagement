using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Modules.Messaging;
using Xunit;

namespace Pawzaroo.Tests.Unit;

public class AdoptionStageRulesTests
{
    [Theory]
    [InlineData(AdoptionApplicationStage.Submitted, AdoptionApplicationStage.ReferenceCheck, true)]
    [InlineData(AdoptionApplicationStage.Submitted, AdoptionApplicationStage.Adopted, false)]
    [InlineData(AdoptionApplicationStage.Decision, AdoptionApplicationStage.Adopted, true)]
    [InlineData(AdoptionApplicationStage.Declined, AdoptionApplicationStage.Decision, false)]
    [InlineData(AdoptionApplicationStage.Adopted, AdoptionApplicationStage.FollowUp, true)]
    [InlineData(AdoptionApplicationStage.HomeVisit, AdoptionApplicationStage.HomeVisit, false)]
    public void Transitions(AdoptionApplicationStage from, AdoptionApplicationStage to, bool ok)
        => Assert.Equal(ok, AdoptionStageRules.CanTransition(from, to));
}

public class AdoptionSlaTests
{
    [Fact]
    public void DefaultsAndBounds()
    {
        Assert.Equal(7, AdoptionSla.NormalizeDays(null));
        Assert.Equal(7, AdoptionSla.NormalizeDays(0));
        Assert.Equal(14, AdoptionSla.NormalizeDays(14));
        Assert.Equal(7, AdoptionSla.NormalizeDays(500));
    }

    [Fact]
    public void OverdueAndReminder()
    {
        var due = new DateTime(2025, 1, 8, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(AdoptionSla.IsOverdue(due, due.AddMinutes(1)));
        Assert.False(AdoptionSla.IsOverdue(due, due.AddMinutes(-1)));
        Assert.True(AdoptionSla.NeedsReminder(due, due.AddHours(-3)));
        Assert.False(AdoptionSla.NeedsReminder(due, due.AddDays(-3)));
        Assert.False(AdoptionSla.NeedsReminder(due, due.AddHours(1)));
    }

    [Fact]
    public void FollowUpSchedule() =>
        Assert.Equal(new[] { 7, 30, 90 }, AdoptionSla.FollowUpSchedule(DateTime.UtcNow).Select(s => s.Offset).ToArray());
}

public class MatchScoringTests
{
    private static AdopterFacts Adopter(bool kids = false, HomeEnvironment? home = HomeEnvironment.House, ActivityLevel a = ActivityLevel.Moderate)
        => new(2, kids ? new[] { 5 } : Array.Empty<int>(), Array.Empty<string>(), AdopterExperience.Some, home, false, 20, a, 23.8, 90.4);

    [Fact]
    public void ChildFriendlyBeatsNot()
    {
        var good = new ListingFacts(AnimalType.Dog, AnimalSize.Medium, true, true, 36, null, 23.8, 90.4);
        var bad = good with { GoodWithChildren = false };
        Assert.True(MatchScoring.Score(Adopter(true), good).Percent > MatchScoring.Score(Adopter(true), bad).Percent);
    }

    [Fact]
    public void ApartmentPenalisesLargeDogs()
    {
        var big = new ListingFacts(AnimalType.Dog, AnimalSize.Large, true, true, 36, null, null, null);
        var small = big with { Size = AnimalSize.Small };
        var a = Adopter(home: HomeEnvironment.Apartment);
        Assert.True(MatchScoring.Score(a, small).Percent > MatchScoring.Score(a, big).Percent);
    }

    [Fact]
    public void FarAwayLowersScoreAndExplains()
    {
        var near = new ListingFacts(AnimalType.Cat, AnimalSize.Small, true, true, 36, null, 23.8, 90.4);
        var far = near with { Lat = 25.5, Lng = 91.9 };
        var rn = MatchScoring.Score(Adopter(), near);
        var rf = MatchScoring.Score(Adopter(), far);
        Assert.True(rn.Percent > rf.Percent);
        Assert.Contains(rf.Explanations, e => e.Contains("km"));
        Assert.InRange(rn.Percent, 0, 100);
    }
}

public class ListingRiskRulesTests
{
    private static readonly DateTime Now = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static RiskCandidate Cand(string title = "Friendly dog", decimal fee = 0, string[]? photos = null, string? phone = null, int count = 1, int ageDays = 100)
        => new(Guid.NewGuid(), Guid.NewGuid(), title, fee, photos ?? new[] { "https://x/a.jpg" }, phone, Now.AddDays(-ageDays), count);

    [Fact]
    public void DuplicateTitlePhotoPhoneAcrossOwners()
    {
        var other = new RiskOther(Guid.NewGuid(), "Friendly  Dog!", new[] { "https://x/a.jpg?w=1" }, "+880 1711-111111", Now.AddDays(-5));
        var flags = ListingRiskRules.Evaluate(Cand(phone: "01711111111"), new[] { other }, Now);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.DuplicateTitle);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.DuplicatePhoto);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.DuplicatePhone);
    }

    [Fact]
    public void SameOwnerOrOldListingsIgnored()
    {
        var c = Cand();
        var sameOwner = new RiskOther(c.OwnerId, c.Title, c.PhotoUrls, null, Now.AddDays(-1));
        var old = new RiskOther(Guid.NewGuid(), c.Title, c.PhotoUrls, null, Now.AddDays(-40));
        Assert.Empty(ListingRiskRules.Evaluate(c, new[] { sameOwner, old }, Now));
    }

    [Fact]
    public void FeeNewAccountAndStock()
    {
        var flags = ListingRiskRules.Evaluate(Cand(fee: 90000, photos: new[] { "https://www.shutterstock.com/x.jpg" }, count: 5, ageDays: 1), Array.Empty<RiskOther>(), Now);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.ExtremeFee);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.NewAccountManyListings);
        Assert.Contains(flags, f => f.Kind == ListingRiskKind.StockPhoto);
    }
}

public class MessagingLogicTests
{
    private static readonly DateTime T = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EditAndRecallWindows()
    {
        Assert.True(MessageWindows.CanEdit(T, T.AddMinutes(14)));
        Assert.False(MessageWindows.CanEdit(T, T.AddMinutes(16)));
        Assert.True(MessageWindows.CanRecall(T, T.AddMinutes(9)));
        Assert.False(MessageWindows.CanRecall(T, T.AddMinutes(11)));
    }

    [Fact]
    public void DisappearingExpiry()
    {
        Assert.True(MessageWindows.IsExpired(T, 3600, T.AddHours(1)));
        Assert.False(MessageWindows.IsExpired(T, 3600, T.AddMinutes(59)));
        Assert.False(MessageWindows.IsExpired(T, null, T.AddYears(1)));
        Assert.True(MessageWindows.IsValidDisappear(86400));
        Assert.False(MessageWindows.IsValidDisappear(5));
    }

    [Fact]
    public void SpamHeuristics_Duplicates()
    {
        Assert.Equal(SpamVerdict.Ok, SpamHeuristics.Assess("hello", 0, false).Verdict);
        Assert.Equal(SpamVerdict.Flag, SpamHeuristics.Assess("hello", 2, false).Verdict);
        Assert.Equal(SpamVerdict.Block, SpamHeuristics.Assess("hello", 4, false).Verdict);
    }

    [Fact]
    public void SpamHeuristics_Links()
    {
        Assert.Equal(3, SpamHeuristics.CountLinks("http://a.com https://b.com www.c.com"));
        Assert.Equal(SpamVerdict.Flag, SpamHeuristics.Assess("http://a.com https://b.com www.c.com", 0, false).Verdict);
        Assert.Equal(SpamVerdict.Flag, SpamHeuristics.Assess("http://a.com http://b.com", 0, true).Verdict);
        Assert.Equal(SpamVerdict.Ok, SpamHeuristics.Assess("http://a.com http://b.com", 0, false).Verdict);
    }

    [Fact]
    public void CoalescingWindow()
    {
        Assert.True(NotificationCoalescing.ShouldCoalesce(T, false, T.AddMinutes(4)));
        Assert.False(NotificationCoalescing.ShouldCoalesce(T, false, T.AddMinutes(6)));
        Assert.False(NotificationCoalescing.ShouldCoalesce(T, true, T.AddMinutes(1)));
        Assert.Equal(3, NotificationCoalescing.ParseCount(NotificationCoalescing.Body(3, "hi")));
        Assert.Equal("hi", NotificationCoalescing.Body(1, "hi"));
    }
}

public class MatchAnimalTypeTests
{
    private static AdopterFacts Adopter(AnimalType? preferred)
        => new(2, Array.Empty<int>(), Array.Empty<string>(), AdopterExperience.Some, HomeEnvironment.House, false, 20, ActivityLevel.Moderate, null, null, preferred);

    [Fact]
    public void PreferredTypeMismatchScoresLowerWithExplanation()
    {
        var cat = new ListingFacts(AnimalType.Cat, AnimalSize.Small, true, true, 36, null, null, null);
        var match = MatchScoring.Score(Adopter(AnimalType.Cat), cat);
        var miss = MatchScoring.Score(Adopter(AnimalType.Dog), cat);
        var none = MatchScoring.Score(Adopter(null), cat);
        Assert.True(match.Percent > miss.Percent);
        Assert.Equal(none.Percent, match.Percent);
        Assert.Contains(miss.Explanations, e => e.Contains("animal type", StringComparison.OrdinalIgnoreCase));
    }
}
