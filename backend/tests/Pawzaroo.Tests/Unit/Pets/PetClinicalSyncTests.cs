using FluentAssertions;
using Pawzaroo.Infrastructure.Modules.Pets;
using Xunit;

namespace Pawzaroo.Tests.Unit.Pets;

public class PetClinicalSyncTests
{
    [Theory]
    [InlineData("once daily", "09:00")]
    [InlineData("OD", "09:00")]
    [InlineData("BID", "09:00,21:00")]
    [InlineData("twice a day", "09:00,21:00")]
    [InlineData("every 12h", "09:00,21:00")]
    [InlineData("TID", "08:00,14:00,20:00")]
    [InlineData("3x daily", "08:00,14:00,20:00")]
    [InlineData("QID", "08:00,12:00,16:00,20:00")]
    [InlineData("1-0-1", "08:00,20:00")]
    [InlineData("0-1-0", "14:00")]
    [InlineData(null, "09:00")]
    public void maps_frequency_notation_to_dose_times(string? frequency, string expected)
        => PetClinicalSync.DoseTimesFor(frequency).Should().Be(expected);

    [Theory]
    [InlineData("7 days", 7)]
    [InlineData("5d", 5)]
    [InlineData("2 weeks", 14)]
    [InlineData("1 month", 30)]
    [InlineData("10", 10)]
    [InlineData("for 3 wk", 21)]
    [InlineData("24 months", 365)]   // capped at a year
    [InlineData("until finished", null)]
    [InlineData("", null)]
    public void parses_duration_to_days(string duration, int? expected)
        => PetClinicalSync.DurationDays(duration).Should().Be(expected);

    [Fact]
    public void parses_prescription_items_case_insensitively_and_skips_blank_drugs()
    {
        const string json = """
        [
          { "Drug": "Amoxicillin", "dose": "250mg", "frequency": "BID", "duration": "7 days", "instructions": "with food" },
          { "name": "Meloxicam", "dose": "0.5ml" },
          { "dose": "orphan dose without a drug" }
        ]
        """;

        var items = PetClinicalSync.ParseItems(json);

        items.Should().HaveCount(2);
        items[0].Should().Be(new PetClinicalSync.RxItem("Amoxicillin", "250mg", "BID", "7 days", "with food"));
        items[1].Drug.Should().Be("Meloxicam");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"drug\":\"x\"}")]   // object, not array
    public void tolerates_missing_or_malformed_items_json(string? json)
        => PetClinicalSync.ParseItems(json).Should().BeEmpty();
}
