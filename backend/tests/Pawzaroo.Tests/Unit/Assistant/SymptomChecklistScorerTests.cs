using FluentAssertions;
using Pawzaroo.Application.Modules.Assistant;
using Pawzaroo.Infrastructure.Modules.Assistant;
using Xunit;

namespace Pawzaroo.Tests.Unit.Assistant;

public class SymptomChecklistScorerTests
{
    private static SymptomChecklist Healthy() => new("dog", "lt24h", "normal", "normal", "pink", "none", 0, 0, "none", false, false, false, null);

    [Fact]
    public void Healthy_checklist_is_routine_but_still_needs_human_review()
    {
        var a = SymptomChecklistScorer.Score(Healthy());
        a.Urgency.Should().Be(TriageUrgency.Routine);
        a.NeedsHumanReview.Should().BeTrue();
        a.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void Photos_add_the_human_review_notice_and_never_lower_urgency()
    {
        var a = SymptomChecklistScorer.Score(Healthy() with { VomitingCount = 1 }, hasPhotos: true);
        a.Urgency.Should().Be(TriageUrgency.Soon);
        a.Summary.Should().Contain("Photo needs human vet review");
        a.NeedsHumanReview.Should().BeTrue();
    }

    [Theory]
    [InlineData("laboured", "pink", "none", false, false, "none", 0)]
    [InlineData("normal", "blue", "none", false, false, "none", 0)]
    [InlineData("normal", "pale", "none", false, false, "none", 0)]
    [InlineData("normal", "pink", "heavy", false, false, "none", 0)]
    [InlineData("normal", "pink", "none", true, false, "none", 0)]
    [InlineData("normal", "pink", "none", false, true, "none", 0)]
    [InlineData("normal", "pink", "none", false, false, "severe", 0)]
    [InlineData("normal", "pink", "none", false, false, "none", 6)]
    public void Emergency_rules(string breathing, string gums, string bleeding, bool seizure, bool toxin, string lethargy, int vomits)
    {
        var c = Healthy() with
        {
            Breathing = breathing, GumColour = gums, Bleeding = bleeding, Seizure = seizure, PossibleToxinExposure = toxin,
            Lethargy = lethargy, VomitingCount = vomits,
        };
        SymptomChecklistScorer.Score(c).Urgency.Should().Be(TriageUrgency.Emergency);
    }

    [Fact]
    public void Emergency_phrase_in_notes_escalates()
    {
        var c = Healthy() with { Notes = "he is having a seizure right now" };
        SymptomChecklistScorer.Score(c).Urgency.Should().Be(TriageUrgency.Emergency);
    }

    [Theory]
    [InlineData("yellow", "normal", 0, 0, "normal", "lt24h", false)]
    [InlineData("pink", "fast", 0, 0, "normal", "lt24h", false)]
    [InlineData("pink", "normal", 3, 0, "normal", "lt24h", false)]
    [InlineData("pink", "normal", 0, 6, "normal", "lt24h", false)]
    [InlineData("pink", "normal", 0, 0, "none", "1to3d", false)]
    [InlineData("pink", "normal", 2, 0, "normal", "lt24h", true)]
    public void Urgent_rules(string gums, string breathing, int vomits, int diarrhoea, string appetite, string duration, bool youngOrSenior)
    {
        var c = Healthy() with
        {
            GumColour = gums, Breathing = breathing, VomitingCount = vomits, DiarrhoeaCount = diarrhoea,
            Appetite = appetite, Duration = duration, YoungOrSenior = youngOrSenior,
        };
        SymptomChecklistScorer.Score(c).Urgency.Should().Be(TriageUrgency.Urgent);
    }

    [Theory]
    [InlineData(1, 0, "normal", "none", "none")]
    [InlineData(0, 2, "normal", "none", "none")]
    [InlineData(0, 0, "reduced", "none", "none")]
    [InlineData(0, 0, "normal", "mild", "none")]
    [InlineData(0, 0, "normal", "none", "minor")]
    public void Soon_rules(int vomits, int diarrhoea, string appetite, string lethargy, string bleeding)
    {
        var c = Healthy() with { VomitingCount = vomits, DiarrhoeaCount = diarrhoea, Appetite = appetite, Lethargy = lethargy, Bleeding = bleeding };
        SymptomChecklistScorer.Score(c).Urgency.Should().Be(TriageUrgency.Soon);
    }

    [Fact]
    public void Highest_rule_wins_and_all_reasons_are_listed()
    {
        var c = Healthy() with { VomitingCount = 3, Seizure = true };
        var a = SymptomChecklistScorer.Score(c);
        a.Urgency.Should().Be(TriageUrgency.Emergency);
        a.Reasons.Should().Contain(r => r.StartsWith("Emergency")).And.Contain(r => r.StartsWith("Urgent"));
    }

    [Fact]
    public void Keywords_are_case_and_separator_insensitive_and_unknown_values_are_mild()
    {
        SymptomChecklistScorer.Score(Healthy() with { Breathing = "LABOURED" }).Urgency.Should().Be(TriageUrgency.Emergency);
        SymptomChecklistScorer.Score(Healthy() with { Breathing = "wheezy?", GumColour = "???", Duration = "forever" })
            .Urgency.Should().Be(TriageUrgency.Routine);
    }

    [Fact]
    public void Negative_counts_are_ignored()
    {
        SymptomChecklistScorer.Score(Healthy() with { VomitingCount = -5, DiarrhoeaCount = -1 }).Urgency.Should().Be(TriageUrgency.Routine);
    }
}
