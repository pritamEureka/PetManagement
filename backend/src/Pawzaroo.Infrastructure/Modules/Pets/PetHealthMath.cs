using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Infrastructure.Modules.Pets;

// Pure, dependency-free logic for the pet-health-plus features. Kept separate from the
// services so it can be unit-tested without a database.

public record GeneratedCareItem(CarePlanItemType Type, string Title, DateTime DueDate, string? Notes);

public record CarePlanBlueprintItem(CarePlanItemType Type, string Title, int OffsetDays, int? RepeatEveryDays, int? RepeatCount, string? Notes);

public static class CarePlanMath
{
    /// <summary>Safety cap on generated occurrences per blueprint item.</summary>
    public const int MaxOccurrences = 60;
    /// <summary>Repeating items without an explicit count are expanded to this horizon.</summary>
    public const int DefaultHorizonDays = 365;

    public static List<GeneratedCareItem> Generate(IEnumerable<CarePlanBlueprintItem> items, DateTime startDate)
    {
        var start = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var result = new List<GeneratedCareItem>();
        foreach (var it in items)
        {
            var first = start.AddDays(Math.Max(0, it.OffsetDays));
            if (it.RepeatEveryDays is not > 0)
            {
                result.Add(new GeneratedCareItem(it.Type, it.Title, first, it.Notes));
                continue;
            }
            var every = it.RepeatEveryDays.Value;
            var count = it.RepeatCount is > 0
                ? Math.Min(it.RepeatCount.Value, MaxOccurrences)
                : Math.Min(Math.Max(1, DefaultHorizonDays / every + 1), MaxOccurrences);
            for (var i = 0; i < count; i++)
                result.Add(new GeneratedCareItem(it.Type, it.Title, first.AddDays((long)i * every), it.Notes));
        }
        return result.OrderBy(r => r.DueDate).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Pending/rescheduled items whose due date has passed (date-only comparison) are "missed".</summary>
    public static bool IsMissed(CarePlanItemStatus status, DateTime dueDate, DateTime nowUtc)
        => status is CarePlanItemStatus.Pending or CarePlanItemStatus.Rescheduled or CarePlanItemStatus.Missed
           && dueDate.Date < nowUtc.Date;
}

public static class SealHasher
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    /// <summary>Deterministic JSON: keys sorted ordinally, no whitespace.</summary>
    public static string Canonical(IReadOnlyDictionary<string, string?> fields)
    {
        var sorted = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var kv in fields) sorted[kv.Key] = kv.Value;
        return JsonSerializer.Serialize(sorted, Json);
    }

    public static string Hash(string canonicalSnapshot, Guid sealedByUserId, DateTime sealedAtUtc)
    {
        var payload = $"{canonicalSnapshot}|{sealedByUserId:D}|{sealedAtUtc.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffffffZ}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(string canonicalSnapshot, Guid sealedByUserId, DateTime sealedAtUtc, string expectedHash)
        => string.Equals(Hash(canonicalSnapshot, sealedByUserId, sealedAtUtc), expectedHash, StringComparison.OrdinalIgnoreCase);

    public static string D(DateTime? d) => d?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) ?? "";

    public static Dictionary<string, string?> Snapshot(VaccinationRecord v) => new()
    {
        ["vaccineName"] = v.VaccineName,
        ["administeredOn"] = D(v.AdministeredOn),
        ["nextDueOn"] = D(v.NextDueOn),
        ["administeredByVet"] = v.AdministeredByVet,
        ["notes"] = v.Notes,
        ["certificateUrl"] = v.CertificateUrl,
        ["petId"] = v.PetId.ToString("D")
    };

    public static Dictionary<string, string?> Snapshot(MedicalRecord m) => new()
    {
        ["visitDate"] = D(m.VisitDate),
        ["diagnosis"] = m.Diagnosis,
        ["treatment"] = m.Treatment,
        ["notes"] = m.Notes,
        ["vetName"] = m.VetName,
        ["attachmentUrl"] = m.AttachmentUrl,
        ["prescriptionFileUrl"] = m.PrescriptionFileUrl,
        ["appointmentId"] = m.AppointmentId?.ToString("D"),
        ["petId"] = m.PetId.ToString("D")
    };
}

public record WeightPoint(DateTime Date, decimal Kg);

public record WeightAnomalyResult(bool IsAnomaly, string? Reason, decimal? PercentChange, double? ZScore);

public record RepeatedVisitResult(string Keyword, int Visits, DateTime FirstVisit, DateTime LastVisit);

public static class InsightMath
{
    /// <summary>Doses that should have been taken in [from, to) for a course (inclusive of start/end dates).</summary>
    public static int ExpectedDoses(DateTime courseStart, DateTime? courseEnd, DateTime from, DateTime to, int timesPerDay)
    {
        if (timesPerDay <= 0) return 0;
        var s = courseStart.Date > from.Date ? courseStart.Date : from.Date;
        var lastDay = to.Date.AddDays(-1);
        var e = courseEnd.HasValue && courseEnd.Value.Date < lastDay ? courseEnd.Value.Date : lastDay;
        if (e < s) return 0;
        return ((e - s).Days + 1) * timesPerDay;
    }

    /// <summary>0..1 fraction of expected doses that were not taken. Null when nothing was expected.</summary>
    public static double? MissedRate(int expected, int taken)
    {
        if (expected <= 0) return null;
        var missed = Math.Max(0, expected - Math.Max(0, taken));
        return Math.Round((double)missed / expected, 4);
    }

    /// <summary>
    /// Flags the newest weight when it deviates sharply from the trailing weights:
    /// |z| >= 2.5 with at least 4 trailing points, or a &gt;=10% change from the trailing mean (or &gt;=8%
    /// between the last two points within 30 days). Optionally also flags weights outside a breed/size healthy range.
    /// </summary>
    public static WeightAnomalyResult DetectWeightAnomaly(IReadOnlyList<WeightPoint> points, decimal? rangeMin = null, decimal? rangeMax = null)
    {
        if (points.Count < 2) return new(false, null, null, null);
        var ordered = points.OrderBy(p => p.Date).ToList();
        var latest = ordered[^1];
        var trailing = ordered.Take(ordered.Count - 1).TakeLast(8).Select(p => (double)p.Kg).ToList();
        var mean = trailing.Average();
        var pct = mean > 0 ? (decimal)((double)latest.Kg - mean) / (decimal)mean * 100m : 0m;
        pct = Math.Round(pct, 1);

        double? z = null;
        if (trailing.Count >= 4)
        {
            var variance = trailing.Sum(x => (x - mean) * (x - mean)) / (trailing.Count - 1);
            var sd = Math.Sqrt(variance);
            // Floor the sd at 2% of the mean so a perfectly flat history doesn't make 0.1 kg "infinite sigma".
            sd = Math.Max(sd, mean * 0.02);
            z = Math.Round(((double)latest.Kg - mean) / sd, 2);
        }

        var prev = ordered[^2];
        var recentPct = prev.Kg > 0 ? Math.Round((latest.Kg - prev.Kg) / prev.Kg * 100m, 1) : 0m;
        var within30 = (latest.Date - prev.Date).TotalDays <= 30;

        string? reason = null;
        if (z is { } zz && Math.Abs(zz) >= 2.5)
            reason = $"Latest weight is {Math.Abs(zz):0.#} standard deviations {(zz > 0 ? "above" : "below")} the recent trend.";
        else if (Math.Abs(pct) >= 10)
            reason = $"Latest weight is {Math.Abs(pct):0.#}% {(pct > 0 ? "above" : "below")} the recent average.";
        else if (within30 && Math.Abs(recentPct) >= 8)
            reason = $"Weight changed {Math.Abs(recentPct):0.#}% ({(recentPct > 0 ? "gain" : "loss")}) since the previous reading.";
        else if (rangeMax is { } mx && latest.Kg > mx)
            reason = $"Latest weight {latest.Kg:0.##} kg is above the typical range for this species/size ({rangeMin:0.##}–{mx:0.##} kg).";
        else if (rangeMin is { } mn && latest.Kg < mn)
            reason = $"Latest weight {latest.Kg:0.##} kg is below the typical range for this species/size ({mn:0.##}–{rangeMax:0.##} kg).";

        return new(reason is not null, reason, pct, z);
    }

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "with","from","that","this","there","have","has","were","was","after","before","mild","severe","acute","chronic",
        "check","checkup","routine","follow","followup","visit","pain","signs","due","and","the","for","dog","cat","pet"
    };

    public static IEnumerable<string> Keywords(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) yield break;
        foreach (Match m in Regex.Matches(reason.ToLowerInvariant(), "[a-z]{4,}"))
            if (!Stop.Contains(m.Value)) yield return m.Value;
    }

    /// <summary>Keywords that appear in at least <paramref name="minVisits"/> distinct visits within a trailing window.</summary>
    public static List<RepeatedVisitResult> RepeatedVisits(IEnumerable<(DateTime Date, string? Reason)> visits,
        DateTime nowUtc, int windowDays = 90, int minVisits = 3)
    {
        var from = nowUtc.AddDays(-windowDays);
        var recent = visits.Where(v => v.Date >= from && v.Date <= nowUtc.AddDays(1)).ToList();
        var byKeyword = new Dictionary<string, HashSet<DateTime>>();
        foreach (var v in recent)
            foreach (var k in Keywords(v.Reason).Distinct())
            {
                if (!byKeyword.TryGetValue(k, out var set)) byKeyword[k] = set = new HashSet<DateTime>();
                set.Add(v.Date);
            }
        return byKeyword.Where(kv => kv.Value.Count >= minVisits)
            .Select(kv => new RepeatedVisitResult(kv.Key, kv.Value.Count, kv.Value.Min(), kv.Value.Max()))
            .OrderByDescending(r => r.Visits).ThenBy(r => r.Keyword, StringComparer.Ordinal).ToList();
    }
}

public static class WithdrawalMath
{
    /// <summary>
    /// First moment the product is considered safe: administration time + N days (N = 0 means no withdrawal).
    /// Always defer to the veterinary label — this is a record-keeping aid, not a legal determination.
    /// </summary>
    public static DateTime? SafeOn(DateTime administeredOn, int withdrawalDays)
        => withdrawalDays <= 0 ? null : administeredOn.AddDays(withdrawalDays);

    public static bool InWithdrawal(DateTime? safeOn, DateTime nowUtc) => safeOn.HasValue && safeOn.Value > nowUtc;

    /// <summary>With several treatments the latest safe date governs.</summary>
    public static DateTime? Latest(IEnumerable<DateTime?> safeDates)
    {
        DateTime? best = null;
        foreach (var d in safeDates) if (d.HasValue && (best is null || d > best)) best = d;
        return best;
    }
}

public static class LabMath
{
    private static readonly Regex Range = new(@"^\s*(-?\d+(?:[.,]\d+)?)\s*(?:-|–|to)\s*(-?\d+(?:[.,]\d+)?)", RegexOptions.Compiled);

    public static bool TryParseRange(string? text, out decimal min, out decimal max)
    {
        min = max = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var m = Range.Match(text);
        if (!m.Success) return false;
        return decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out min)
            && decimal.TryParse(m.Groups[2].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out max)
            && min <= max;
    }

    /// <summary>Flag from a numeric value and "min-max" reference range; Critical when &gt;50% of the range width outside it.</summary>
    public static LabResultFlag ComputeFlag(string? value, string? referenceRange)
    {
        if (string.IsNullOrWhiteSpace(value) || !TryParseRange(referenceRange, out var min, out var max)) return LabResultFlag.Unknown;
        if (!decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return LabResultFlag.Unknown;
        var width = Math.Max(max - min, 0.0001m);
        if (v < min) return min - v > width * 0.5m ? LabResultFlag.Critical : LabResultFlag.Low;
        if (v > max) return v - max > width * 0.5m ? LabResultFlag.Critical : LabResultFlag.High;
        return LabResultFlag.Normal;
    }
}

public record ParsedAllergy(string Allergen, AllergyCategory Category);

public record ParsedDiet(string Food, string? Amount, string? Frequency);

/// <summary>Best-effort parsing of the legacy free-text allergy / diet fields into typed rows.</summary>
public static class HealthTextParser
{
    private static readonly string[] DrugWords = { "penicillin", "amoxicillin", "antibiotic", "sulfa", "nsaid", "ivermectin", "vaccine", "cillin", "mycin", "medication", "drug" };
    private static readonly string[] EnvWords = { "pollen", "dust", "mold", "mould", "grass", "flea", "mite", "smoke", "perfume", "cleaner", "latex", "bite", "tree", "weed" };
    private static readonly string[] FoodWords = { "chicken", "beef", "pork", "fish", "salmon", "egg", "dairy", "milk", "wheat", "grain", "gluten", "soy", "corn", "lamb", "turkey", "rice", "peanut", "treat", "food", "kibble", "cheese" };
    private static readonly Regex Splitter = new(@"[,;\n\r/]+|\s+&\s+|\s+and\s+|\s+\+\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Freq = new(@"(\d+\s*x\s*(?:a\s*)?(?:day|daily|week|weekly)|once\s+(?:a\s+)?(?:day|daily|week|weekly)|twice\s+(?:a\s+)?(?:day|daily|week)|three\s+times\s+(?:a\s+)?(?:day|daily)|every\s+\d+\s*(?:h|hours?|days?)|daily|weekly|morning|evening|night)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Amt = new(@"(\d+(?:[.,]\d+)?\s*(?:g|gm|grams?|kg|ml|l|cups?|tbsp|tsp|oz|cans?|scoops?|pieces?))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ParsedAllergy> ParseAllergies(string? text)
    {
        var list = new List<ParsedAllergy>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var raw in Splitter.Split(text))
        {
            var t = raw.Trim().Trim('.', '-', '•', '*', ' ');
            if (t.Length < 2 || t.Length > 120) continue;
            if (Regex.IsMatch(t, @"^(none|no|nil|n/?a|nka|nkda|no known allergies)\b", RegexOptions.IgnoreCase)) continue;
            if (list.Any(a => a.Allergen.Equals(t, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(new ParsedAllergy(t, Categorise(t)));
        }
        return list;
    }

    public static AllergyCategory Categorise(string allergen)
    {
        var l = allergen.ToLowerInvariant();
        if (DrugWords.Any(l.Contains)) return AllergyCategory.Drug;
        if (EnvWords.Any(l.Contains)) return AllergyCategory.Environmental;
        if (FoodWords.Any(l.Contains)) return AllergyCategory.Food;
        return AllergyCategory.Other;
    }

    public static List<ParsedDiet> ParseDiet(string? text)
    {
        var list = new List<ParsedDiet>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var raw in Regex.Split(text, @"[;\n\r]+|\.\s+"))
        {
            var t = raw.Trim().Trim('.', '-', '•', '*', ' ');
            if (t.Length < 2) continue;
            var freq = Freq.Match(t);
            var amt = Amt.Match(t);
            var food = t;
            if (freq.Success) food = food.Replace(freq.Value, " ", StringComparison.OrdinalIgnoreCase);
            if (amt.Success) food = food.Replace(amt.Value, " ", StringComparison.OrdinalIgnoreCase);
            food = Regex.Replace(food, @"\s+", " ").Trim(' ', ',', '-', ':', '(', ')');
            if (food.Length == 0) food = t;
            if (food.Length > 200) food = food[..200];
            list.Add(new ParsedDiet(food, amt.Success ? amt.Value.Trim() : null, freq.Success ? freq.Value.Trim() : null));
        }
        return list;
    }
}
