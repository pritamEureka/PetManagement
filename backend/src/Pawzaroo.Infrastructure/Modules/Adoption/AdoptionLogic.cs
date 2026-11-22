using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

/// <summary>Allowed pipeline moves. Declined/Withdrawn/FollowUp are terminal.</summary>
public static class AdoptionStageRules
{
    private static readonly Dictionary<AdoptionApplicationStage, AdoptionApplicationStage[]> Allowed = new()
    {
        [AdoptionApplicationStage.Submitted] = new[] { AdoptionApplicationStage.ReferenceCheck, AdoptionApplicationStage.HomeVisit, AdoptionApplicationStage.Decision, AdoptionApplicationStage.Declined, AdoptionApplicationStage.Withdrawn },
        [AdoptionApplicationStage.ReferenceCheck] = new[] { AdoptionApplicationStage.HomeVisit, AdoptionApplicationStage.Decision, AdoptionApplicationStage.Declined, AdoptionApplicationStage.Withdrawn },
        [AdoptionApplicationStage.HomeVisit] = new[] { AdoptionApplicationStage.Decision, AdoptionApplicationStage.Declined, AdoptionApplicationStage.Withdrawn },
        [AdoptionApplicationStage.Decision] = new[] { AdoptionApplicationStage.Adopted, AdoptionApplicationStage.Declined, AdoptionApplicationStage.Withdrawn },
        [AdoptionApplicationStage.Adopted] = new[] { AdoptionApplicationStage.FollowUp },
        [AdoptionApplicationStage.FollowUp] = Array.Empty<AdoptionApplicationStage>(),
        [AdoptionApplicationStage.Declined] = Array.Empty<AdoptionApplicationStage>(),
        [AdoptionApplicationStage.Withdrawn] = Array.Empty<AdoptionApplicationStage>(),
    };

    public static bool CanTransition(AdoptionApplicationStage from, AdoptionApplicationStage to)
        => from != to && Allowed[from].Contains(to);

    public static bool IsTerminal(AdoptionApplicationStage s)
        => s is AdoptionApplicationStage.Declined or AdoptionApplicationStage.Withdrawn or AdoptionApplicationStage.FollowUp;

    public static string NotificationBody(AdoptionApplicationStage stage, string petLabel) => stage switch
    {
        AdoptionApplicationStage.ReferenceCheck => $"Your application for {petLabel} moved to reference checks.",
        AdoptionApplicationStage.HomeVisit => $"A home visit is the next step for your application for {petLabel}.",
        AdoptionApplicationStage.Decision => $"Your application for {petLabel} is under final decision.",
        AdoptionApplicationStage.Adopted => $"Congratulations! Your adoption of {petLabel} is confirmed.",
        AdoptionApplicationStage.FollowUp => $"Follow-up check-ins for {petLabel} have started.",
        AdoptionApplicationStage.Declined => $"Your application for {petLabel} was not selected this time.",
        AdoptionApplicationStage.Withdrawn => $"Your application for {petLabel} was withdrawn.",
        _ => $"Your application for {petLabel} was received."
    };
}

public static class AdoptionSla
{
    public const int DefaultDecisionSlaDays = 7;
    public static readonly int[] FollowUpOffsetsDays = { 7, 30, 90 };

    public static int NormalizeDays(int? configured) => configured is > 0 and <= 90 ? configured.Value : DefaultDecisionSlaDays;
    public static DateTime DecisionDue(DateTime enteredAt, int? slaDays) => enteredAt.AddDays(NormalizeDays(slaDays));
    public static bool IsOverdue(DateTime? dueAt, DateTime now) => dueAt is { } d && now > d;
    /// <summary>True when within 24h of the deadline and not yet overdue.</summary>
    public static bool NeedsReminder(DateTime? dueAt, DateTime now) => dueAt is { } d && now <= d && d - now <= TimeSpan.FromHours(24);
    public static IReadOnlyList<(int Offset, DateTime Due)> FollowUpSchedule(DateTime adoptedAt)
        => FollowUpOffsetsDays.Select(o => (o, adoptedAt.AddDays(o))).ToList();
}

public record AdopterFacts(int HouseholdSize, IReadOnlyList<int> ChildrenAges, IReadOnlyList<string> ExistingPetTypes,
    AdopterExperience Experience, HomeEnvironment? HomeType, bool HasYard, int TravelDistanceKm, ActivityLevel Activity,
    double? Lat, double? Lng, AnimalType? PreferredType = null);

public record ListingFacts(AnimalType AnimalType, AnimalSize? Size, bool? GoodWithChildren, bool? GoodWithOtherPets,
    int? AgeMonths, string? BehaviorNotes, double? Lat, double? Lng);

public record MatchResult(int Percent, IReadOnlyList<string> Explanations);

public static class MatchScoring
{
    // weights sum to 100
    private const int WSize = 20, WEnergy = 15, WChildren = 20, WPets = 15, WExperience = 15, WDistance = 15;

    public static MatchResult Score(AdopterFacts a, ListingFacts l)
    {
        double total = 0;
        var why = new List<string>();

        double size = 0.7;
        if (l.Size is { } s)
        {
            var big = s >= AnimalSize.Large;
            if (a.HomeType == HomeEnvironment.Apartment) size = big ? 0.2 : 1.0;
            else if (a.HomeType is HomeEnvironment.HouseWithYard or HomeEnvironment.Farm || a.HasYard) size = 1.0;
            else size = big ? 0.6 : 0.9;
            why.Add(size >= 0.8 ? "Size suits your home" : "Size may be a stretch for your home");
        }
        total += size * WSize;

        var energy = EstimateEnergy(l);
        var eScore = 1.0 - Math.Abs((int)energy - (int)a.Activity) * 0.4;
        total += Math.Max(0, eScore) * WEnergy;
        why.Add(eScore >= 0.8 ? "Energy level matches your activity" : "Energy level differs from your lifestyle");

        double kids = 0.8;
        if (a.ChildrenAges.Count > 0)
        {
            kids = l.GoodWithChildren switch { true => 1.0, false => 0.0, _ => 0.5 };
            why.Add(l.GoodWithChildren switch { true => "Good with children", false => "Not recommended for homes with children", _ => "Child compatibility unknown" });
        }
        total += kids * WChildren;

        double pets = 0.8;
        if (a.ExistingPetTypes.Count > 0)
        {
            pets = l.GoodWithOtherPets switch { true => 1.0, false => 0.1, _ => 0.5 };
            why.Add(l.GoodWithOtherPets switch { true => "Good with other pets", false => "Prefers to be the only pet", _ => "Pet compatibility unknown" });
        }
        total += pets * WPets;

        double exp = a.Experience switch { AdopterExperience.Experienced => 1.0, AdopterExperience.Some => 0.8, _ => 0.6 };
        if (a.Experience == AdopterExperience.None
            && (l.Size >= AnimalSize.Large || l.BehaviorNotes?.Contains("special", StringComparison.OrdinalIgnoreCase) == true))
            exp = 0.3;
        total += exp * WExperience;
        why.Add(exp >= 0.8 ? "Your experience fits" : "More experience may be helpful");

        double dist = 0.6;
        if (a.Lat is { } la && a.Lng is { } lo && l.Lat is { } ll && l.Lng is { } lg)
        {
            var km = GeoSearch.DistanceKm(la, lo, ll, lg);
            var willing = Math.Max(1, a.TravelDistanceKm);
            dist = km <= willing ? 1.0 - 0.3 * (km / willing) : Math.Max(0, 0.7 - (km - willing) / (willing * 2.0));
            why.Add($"About {Math.Round(km)} km away (you travel up to {willing} km)");
        }
        total += dist * WDistance;

        if (a.PreferredType is { } pt)
        {
            if (pt == l.AnimalType) why.Insert(0, $"Matches the animal type you're looking for ({l.AnimalType})");
            else
            {
                total *= 0.4;
                why.Insert(0, $"Different animal type than you're looking for ({pt}); this is a {l.AnimalType}");
            }
        }

        return new MatchResult((int)Math.Round(Math.Clamp(total, 0, 100)), why);
    }

    public static ActivityLevel EstimateEnergy(ListingFacts l)
    {
        var notes = l.BehaviorNotes ?? "";
        if (notes.Contains("energetic", StringComparison.OrdinalIgnoreCase) || notes.Contains("active", StringComparison.OrdinalIgnoreCase)) return ActivityLevel.High;
        if (notes.Contains("calm", StringComparison.OrdinalIgnoreCase) || notes.Contains("quiet", StringComparison.OrdinalIgnoreCase)) return ActivityLevel.Low;
        if (l.AgeMonths is < 24) return ActivityLevel.High;
        if (l.AgeMonths is > 96) return ActivityLevel.Low;
        return ActivityLevel.Moderate;
    }
}

public record RiskCandidate(Guid ListingId, Guid OwnerId, string Title, decimal Fee, IReadOnlyList<string> PhotoUrls, string? Phone,
    DateTime OwnerCreatedAt, int OwnerListingCount);

public record RiskOther(Guid OwnerId, string Title, IReadOnlyList<string> PhotoUrls, string? Phone, DateTime CreatedAt);

public record RiskFlagResult(ListingRiskKind Kind, string Detail, int Severity);

public static class ListingRiskRules
{
    public const int WindowDays = 30;
    public const decimal ExtremeFee = 50000m;
    public static readonly string[] StockHosts =
        { "shutterstock.", "istockphoto.", "gettyimages.", "unsplash.com", "pexels.com", "stock.adobe.", "depositphotos.", "pixabay.com" };

    public static string NormalizeTitle(string t) =>
        new string(t.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static string PhotoKey(string url)
    {
        var u = url.Trim().ToLowerInvariant();
        var q = u.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) u = u[..q];
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(u)));
    }

    public static string? NormalizePhone(string? p)
    {
        if (string.IsNullOrWhiteSpace(p)) return null;
        var d = new string(p.Where(char.IsDigit).ToArray());
        return d.Length >= 7 ? d.Substring(Math.Max(0, d.Length - 10)) : null;
    }

    public static IReadOnlyList<RiskFlagResult> Evaluate(RiskCandidate c, IEnumerable<RiskOther> others, DateTime now)
    {
        var flags = new List<RiskFlagResult>();
        var recent = others.Where(o => o.OwnerId != c.OwnerId && now - o.CreatedAt <= TimeSpan.FromDays(WindowDays)).ToList();

        var nt = NormalizeTitle(c.Title);
        if (nt.Length >= 6 && recent.Any(o => NormalizeTitle(o.Title) == nt))
            flags.Add(new(ListingRiskKind.DuplicateTitle, "Same title used by a different owner in the last 30 days", 2));

        var keys = c.PhotoUrls.Select(PhotoKey).ToHashSet();
        if (keys.Count > 0 && recent.Any(o => o.PhotoUrls.Any(p => keys.Contains(PhotoKey(p)))))
            flags.Add(new(ListingRiskKind.DuplicatePhoto, "A photo is also used by a different owner's listing", 3));

        var ph = NormalizePhone(c.Phone);
        if (ph is not null && recent.Any(o => NormalizePhone(o.Phone) == ph))
            flags.Add(new(ListingRiskKind.DuplicatePhone, "Owner phone matches a different owner's listing", 3));

        if (c.Fee >= ExtremeFee)
            flags.Add(new(ListingRiskKind.ExtremeFee, $"Unusually high adoption fee ({c.Fee:0})", 2));

        if (now - c.OwnerCreatedAt <= TimeSpan.FromDays(3) && c.OwnerListingCount >= 3)
            flags.Add(new(ListingRiskKind.NewAccountManyListings, $"Account under 3 days old with {c.OwnerListingCount} listings", 2));

        if (c.PhotoUrls.Any(p => StockHosts.Any(h => p.Contains(h, StringComparison.OrdinalIgnoreCase))))
            flags.Add(new(ListingRiskKind.StockPhoto, "Photo URL points to a stock-photo site", 2));

        return flags;
    }
}
