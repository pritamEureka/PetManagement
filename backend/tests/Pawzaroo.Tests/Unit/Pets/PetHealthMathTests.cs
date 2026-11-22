using FluentAssertions;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Modules.Pets;
using Xunit;

namespace Pawzaroo.Tests.Unit.Pets;

public class PetHealthMathTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void care_plan_generates_one_off_and_repeating_items()
    {
        var items = CarePlanMath.Generate(new[]
        {
            new CarePlanBlueprintItem(CarePlanItemType.Vaccination, "Rabies", 14, null, null, null),
            new CarePlanBlueprintItem(CarePlanItemType.ParasitePrevention, "Flea", 0, 30, 3, "monthly")
        }, Start);
        items.Should().HaveCount(4);
        items.Select(i => i.DueDate).Should().BeInAscendingOrder();
        items.Where(i => i.Title == "Flea").Select(i => i.DueDate).Should()
            .Equal(Start, Start.AddDays(30), Start.AddDays(60));
        items.Single(i => i.Title == "Rabies").DueDate.Should().Be(Start.AddDays(14));
    }

    [Fact]
    public void care_plan_repeat_without_count_is_capped()
    {
        var items = CarePlanMath.Generate(new[] { new CarePlanBlueprintItem(CarePlanItemType.Grooming, "Bath", 0, 1, null, null) }, Start);
        items.Count.Should().Be(CarePlanMath.MaxOccurrences);
    }

    [Fact]
    public void missed_means_past_due_and_not_done()
    {
        var now = Start.AddDays(10);
        CarePlanMath.IsMissed(CarePlanItemStatus.Pending, Start, now).Should().BeTrue();
        CarePlanMath.IsMissed(CarePlanItemStatus.Done, Start, now).Should().BeFalse();
        CarePlanMath.IsMissed(CarePlanItemStatus.Pending, now, now).Should().BeFalse();
    }

    [Fact]
    public void seal_hash_is_deterministic_and_tamper_evident()
    {
        var uid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var at = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var a = SealHasher.Canonical(new Dictionary<string, string?> { ["b"] = "2", ["a"] = "1" });
        var b = SealHasher.Canonical(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" });
        a.Should().Be(b);
        var h1 = SealHasher.Hash(a, uid, at);
        h1.Should().Be(SealHasher.Hash(b, uid, at)).And.HaveLength(64);
        SealHasher.Verify(a, uid, at, h1).Should().BeTrue();
        SealHasher.Hash(a + " ", uid, at).Should().NotBe(h1);
        SealHasher.Hash(a, Guid.NewGuid(), at).Should().NotBe(h1);
        SealHasher.Hash(a, uid, at.AddSeconds(1)).Should().NotBe(h1);
    }

    [Fact]
    public void withdrawal_dates()
    {
        var on = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        WithdrawalMath.SafeOn(on, 0).Should().BeNull();
        WithdrawalMath.SafeOn(on, 7).Should().Be(on.AddDays(7));
        WithdrawalMath.InWithdrawal(on.AddDays(7), on.AddDays(3)).Should().BeTrue();
        WithdrawalMath.InWithdrawal(on.AddDays(7), on.AddDays(8)).Should().BeFalse();
        WithdrawalMath.Latest(new DateTime?[] { null, on.AddDays(2), on.AddDays(9) }).Should().Be(on.AddDays(9));
    }

    [Fact]
    public void missed_dose_rate()
    {
        var from = Start; var to = Start.AddDays(14);
        InsightMath.ExpectedDoses(Start.AddDays(-5), null, from, to, 2).Should().Be(28);
        InsightMath.ExpectedDoses(Start, Start.AddDays(3), from, to, 1).Should().Be(4);
        InsightMath.MissedRate(28, 21).Should().Be(0.25);
        InsightMath.MissedRate(0, 0).Should().BeNull();
    }

    [Fact]
    public void weight_anomaly_detects_sudden_change_but_not_noise()
    {
        var pts = Enumerable.Range(0, 6).Select(i => new WeightPoint(Start.AddDays(i * 30), 20m + (i % 2) * 0.1m)).ToList();
        InsightMath.DetectWeightAnomaly(pts).IsAnomaly.Should().BeFalse();
        pts.Add(new WeightPoint(Start.AddDays(200), 24m));
        var r = InsightMath.DetectWeightAnomaly(pts);
        r.IsAnomaly.Should().BeTrue();
        r.PercentChange.Should().BeGreaterThan(10);
    }

    [Fact]
    public void repeated_visits_need_three_in_ninety_days()
    {
        var now = Start.AddDays(100);
        var visits = new (DateTime, string?)[]
        {
            (now.AddDays(-10), "Ear infection"), (now.AddDays(-40), "Recurring ear infection"), (now.AddDays(-70), "ear infection again"),
            (now.AddDays(-200), "ear infection")
        };
        var r = InsightMath.RepeatedVisits(visits, now);
        r.Should().Contain(x => x.Keyword == "infection" && x.Visits == 3);
        InsightMath.RepeatedVisits(visits.Take(2), now).Should().BeEmpty();
    }

    [Theory]
    [InlineData("12", "10-20", LabResultFlag.Normal)]
    [InlineData("8", "10-20", LabResultFlag.Low)]
    [InlineData("22", "10-20", LabResultFlag.High)]
    [InlineData("40", "10-20", LabResultFlag.Critical)]
    [InlineData("abc", "10-20", LabResultFlag.Unknown)]
    public void lab_flag(string v, string range, LabResultFlag expected) => LabMath.ComputeFlag(v, range).Should().Be(expected);

    [Fact]
    public void legacy_text_parsing()
    {
        var a = HealthTextParser.ParseAllergies("Chicken, penicillin and pollen; none");
        a.Select(x => x.Allergen).Should().Equal("Chicken", "penicillin", "pollen");
        a.Select(x => x.Category).Should().Equal(AllergyCategory.Food, AllergyCategory.Drug, AllergyCategory.Environmental);
        HealthTextParser.ParseAllergies("None").Should().BeEmpty();
        var d = HealthTextParser.ParseDiet("Royal Canin 200g twice daily; boiled rice");
        d.Should().HaveCount(2);
        d[0].Amount.Should().Be("200g");
        d[0].Frequency.Should().Be("twice daily");
    }
}
