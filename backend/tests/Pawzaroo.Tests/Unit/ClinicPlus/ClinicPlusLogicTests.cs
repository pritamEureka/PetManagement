using FluentAssertions;
using Pawzaroo.Infrastructure.Features;
using Xunit;

namespace Pawzaroo.Tests.Unit.ClinicPlus;

public class ClinicPlusLogicTests
{
    private static readonly DateTime Visit = new(2030, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Deposit_fixed_wins_and_is_capped_by_fee()
    {
        BookingPolicyMath.DepositDue(100, 20, null).Should().Be(20m);
        BookingPolicyMath.DepositDue(100, 20, 35).Should().Be(35m);
        BookingPolicyMath.DepositDue(30, null, 50).Should().Be(30m);
        BookingPolicyMath.DepositDue(0, 50, null).Should().Be(0m);
    }

    [Fact]
    public void Cancellation_outside_window_refunds_everything()
    {
        var r = BookingPolicyMath.Cancellation(Visit, Visit.AddHours(-48), 24, 50, 100, 20, byDoctor: false);
        r.IsLate.Should().BeFalse();
        r.Fee.Should().Be(0); r.Refund.Should().Be(20);
    }

    [Fact]
    public void Late_cancellation_takes_fee_from_deposit_and_reports_shortfall()
    {
        var r = BookingPolicyMath.Cancellation(Visit, Visit.AddHours(-2), 24, 50, 100, 20, byDoctor: false);
        r.IsLate.Should().BeTrue();
        r.Fee.Should().Be(50); r.Forfeited.Should().Be(20); r.Refund.Should().Be(0); r.Outstanding.Should().Be(30);

        var small = BookingPolicyMath.Cancellation(Visit, Visit.AddHours(-2), 24, 10, 100, 40, byDoctor: false);
        small.Forfeited.Should().Be(10); small.Refund.Should().Be(30); small.Outstanding.Should().Be(0);
    }

    [Fact]
    public void Doctor_cancellation_is_never_penalised()
    {
        var r = BookingPolicyMath.Cancellation(Visit, Visit.AddMinutes(-5), 24, 100, 100, 20, byDoctor: true);
        r.IsLate.Should().BeFalse(); r.Fee.Should().Be(0); r.Refund.Should().Be(20);
    }

    [Fact]
    public void NoShow_fee_is_a_percentage_of_the_amount()
    {
        var r = BookingPolicyMath.NoShow(25, 80, 20);
        r.Fee.Should().Be(20); r.Forfeited.Should().Be(20); r.Refund.Should().Be(0);
    }

    [Fact]
    public void Signature_is_deterministic_and_order_insensitive_for_codes()
    {
        var doc = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var a = EncounterSignature.Hash(EncounterSignature.Canonical("cc", "f", "dx", "tp",
            new[] { ("B1", "VeNom"), ("A1", "SNOMED") }, doc, Visit, 1));
        var b = EncounterSignature.Hash(EncounterSignature.Canonical("cc ", "f", "dx", "tp",
            new[] { ("a1", "snomed"), ("B1", "VeNom") }, doc, Visit, 1));
        a.Should().Be(b).And.HaveLength(64);
    }

    [Fact]
    public void Signature_changes_when_content_doctor_time_or_version_changes()
    {
        var doc = Guid.NewGuid();
        string H(string dx, Guid d, DateTime t, int v) => EncounterSignature.Hash(EncounterSignature.Canonical(null, null, dx, "tp", Array.Empty<(string, string)>(), d, t, v));
        var baseHash = H("dx", doc, Visit, 1);
        H("dx2", doc, Visit, 1).Should().NotBe(baseHash);
        H("dx", Guid.NewGuid(), Visit, 1).Should().NotBe(baseHash);
        H("dx", doc, Visit.AddSeconds(1), 1).Should().NotBe(baseHash);
        H("dx", doc, Visit, 2).Should().NotBe(baseHash);
    }

    [Fact]
    public void Return_rate_counts_patients_with_two_visits_inside_window()
    {
        var p1 = Guid.NewGuid(); var p2 = Guid.NewGuid(); var p3 = Guid.NewGuid();
        var visits = new[]
        {
            (p1, Visit), (p1, Visit.AddDays(30)),          // returns within 90
            (p2, Visit), (p2, Visit.AddDays(120)),         // returns within 180 only
            (p3, Visit)
        };
        var r90 = ReturnRateMath.Compute(visits, 90);
        r90.Patients.Should().Be(3); r90.Returning.Should().Be(1); r90.Rate.Should().BeApproximately(0.3333, 0.0001);
        ReturnRateMath.Compute(visits, 180).Returning.Should().Be(2);
        ReturnRateMath.Compute(Array.Empty<(Guid, DateTime)>(), 90).Rate.Should().Be(0);
    }

    [Fact]
    public void Emergency_ordering_puts_24x7_first_then_nearest_then_city()
    {
        var far24 = new EmergencyCandidate(Guid.NewGuid(), true, false, "Delhi", 28.9, 77.9, true);
        var near24 = new EmergencyCandidate(Guid.NewGuid(), true, false, "Delhi", 28.62, 77.21, true);
        var nearDay = new EmergencyCandidate(Guid.NewGuid(), false, true, "Delhi", 28.61, 77.20, true);
        var noCoordCity = new EmergencyCandidate(Guid.NewGuid(), false, true, "Delhi", null, null, true);
        var other = new EmergencyCandidate(Guid.NewGuid(), false, true, "Pune", null, null, true);

        var ordered = EmergencyOrdering.Order(new[] { other, noCoordCity, nearDay, far24, near24 }, 28.6139, 77.2090, "Delhi")
            .Select(r => r.Candidate).ToList();
        ordered.Should().Equal(near24, far24, nearDay, noCoordCity, other);
    }

    [Fact]
    public void Emergency_ordering_without_coordinates_uses_city_match()
    {
        var a = new EmergencyCandidate(Guid.NewGuid(), false, true, "Pune", null, null, true);
        var b = new EmergencyCandidate(Guid.NewGuid(), false, true, "delhi", null, null, true);
        EmergencyOrdering.Order(new[] { a, b }, null, null, "Delhi").First().Candidate.Should().Be(b);
    }

    [Theory]
    [InlineData("+91 98765-43210", "tel:+919876543210")]
    [InlineData("(011) 2345 6789", "tel:01123456789")]
    [InlineData("abc", null)]
    public void Tel_uri_is_sanitised(string input, string? expected) => EmergencyVetService.ToTelUri(input).Should().Be(expected);

    [Fact]
    public void Clinic_mates_group_case_insensitively_and_ignore_blank_names()
    {
        var me = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var profiles = new (Guid, string?)[] { (me, " Happy Paws "), (a, "happy paws"), (b, "Other Clinic"), (c, null) };
        var mates = ClinicAnalyticsService.ClinicMates(profiles, me);
        mates.Should().Equal(me, a);

        ClinicAnalyticsService.ClinicMates(new (Guid, string?)[] { (me, "  "), (a, "  ") }, me).Should().BeEmpty();
        ClinicAnalyticsService.ClinicMates(new (Guid, string?)[] { (a, "X") }, me).Should().BeEmpty();
    }
}
