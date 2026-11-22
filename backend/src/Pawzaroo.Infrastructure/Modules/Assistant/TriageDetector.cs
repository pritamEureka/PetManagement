using System.Text.RegularExpressions;

namespace Pawzaroo.Infrastructure.Modules.Assistant;

public record TriageResult(bool IsEmergency, string? Category, string? Matched)
{
    public static readonly TriageResult None = new(false, null, null);
}

/// <summary>
/// Deterministic emergency-symptom detection. Deliberately biased toward over-escalation:
/// a false positive costs a vet call, a false negative can cost a life. Simple negations
/// ("no seizures", "never bleeding") are ignored; general questions such as
/// "which foods are poisonous to dogs?" are not treated as emergencies.
/// </summary>
public static class TriageDetector
{
    private const string Neg = @"(?<!\b(?:no|not|never|without|isn't|wasn't|hasn't|haven't|zero)\s)";
    // "no seizures", "never had a seizure": negator within ~13 chars before, same clause. Kept narrow on purpose.
    private const string NegNear = @"(?<!\b(?:no|never|without|zero)\b[^.!?,;]{0,13})";

    private static readonly (string Category, Regex Pattern)[] Rules =
    {
        ("poisoning", Rx(NegNear + @"\b(poison(ed|ing)|poisoning|intoxicat\w*|antifreeze|rat\s*poison|rodenticide|xylitol|"
            + @"(ate|eaten|swallowed|ingested|licked|chewed)\b.{0,40}\b(chocolate|grapes|raisins|onions?|garlic|medication|pills?|"
            + @"bleach|detergent|battery|lily|lilies|cleaning|pesticide|insecticide|snail bait|paracetamol|ibuprofen)\b)")),
        ("seizure", Rx(NegNear + @"\b(seizures?|seizing|convuls\w*|having a fit|epileptic fit)\b")),
        ("not breathing", Rx(@"\b(not breathing|stopped breathing|isn't breathing|won't breathe|can't breathe|cannot breathe|"
            + @"struggling to breathe|gasping|choking|blue gums|blue tongue|pale gums|no pulse|no heartbeat)\b")),
        ("bleeding", Rx(NegNear + @"\b(heavy bleeding|bleeding heavily|bleeding a lot|bleeding badly|won't stop bleeding|"
            + @"not stop bleeding|blood loss|bleeding profusely|spurting blood|vomiting blood|blood in (the )?vomit|bloody diarrh?oea|"
            + @"bloody diarrhea|coughing up blood)\b")),
        ("bloat", Rx(Neg + @"\b(bloat(ed)?|gdv|swollen (hard )?(belly|stomach|abdomen)|distended (belly|stomach|abdomen)|"
            + @"retching (but )?(nothing|unproductive)|unproductive retching)\b")),
        ("heatstroke", Rx(Neg + @"\b(heat ?stroke|overheated|overheating|collapsed in the heat|left in (a |the )?(hot )?car|"
            + @"panting heavily and (drooling|collapsed|weak))\b")),
        ("collapse", Rx(@"\b(collapsed|unconscious|unresponsive|can't stand|cannot stand|won't wake|not waking|paralys\w*|limp and)\b")),
        ("trauma", Rx(@"\b(hit by (a )?(car|vehicle|bike|truck)|run over|fell from|broken (leg|bone|back|spine)|"
            + @"attacked by (a )?(dog|animal)|deep wound|exposed bone|eye (popped|out of)|snake ?bite|bitten by (a )?snake)\b")),
        ("obstruction", Rx(@"\b(can't (pee|urinate)|cannot (pee|urinate)|straining to (pee|urinate)|blocked (bladder|urethra)|"
            + @"swallowed (a )?(sock|toy|bone|string|needle|corn ?cob)|stuck in (his|her|its) throat)\b")),
        ("birthing", Rx(@"\b(difficulty giving birth|dystocia|stuck (puppy|kitten|baby|calf|lamb|foal)|straining.{0,20}(no|nothing).{0,15}(puppy|kitten|baby|born)|prolapse)\b")),
    };

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    public static TriageResult Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return TriageResult.None;
        var t = text.Replace('’', '\'').Replace('‘', '\'');
        foreach (var (category, pattern) in Rules)
        {
            try
            {
                var m = pattern.Match(t);
                if (m.Success) return new TriageResult(true, category, m.Value);
            }
            catch (RegexMatchTimeoutException) { /* treat as no match for this rule, keep scanning */ }
        }
        return TriageResult.None;
    }
}
