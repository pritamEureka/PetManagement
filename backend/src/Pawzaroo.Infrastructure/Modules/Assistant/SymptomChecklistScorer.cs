using Pawzaroo.Application.Modules.Assistant;

namespace Pawzaroo.Infrastructure.Modules.Assistant;

/// <summary>
/// Deterministic urgency scoring for the symptom checklist. No model is involved: every level is reached by an
/// explicit rule and listed in <see cref="TriageAssessment.Reasons"/>. The result is the highest level any rule
/// reaches, and over-escalation is preferred. A photo (if any) never lowers the level and
/// <see cref="TriageAssessment.NeedsHumanReview"/> is always true.
/// </summary>
public static class SymptomChecklistScorer
{
    public const string PhotoNotice = "Photo needs human vet review - the assistant cannot interpret images.";

    private static string N(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");

    private static int Rank(string urgency) => urgency switch
    {
        TriageUrgency.Emergency => 3,
        TriageUrgency.Urgent => 2,
        TriageUrgency.Soon => 1,
        _ => 0,
    };

    public static TriageAssessment Score(SymptomChecklist c, bool hasPhotos = false)
    {
        var level = 0;
        var reasons = new List<string>();
        void Raise(string urgency, string reason)
        {
            level = Math.Max(level, Rank(urgency));
            reasons.Add($"{urgency}: {reason}");
        }

        var duration = N(c.Duration);
        var appetite = N(c.Appetite);
        var breathing = N(c.Breathing);
        var gums = N(c.GumColour);
        var bleeding = N(c.Bleeding);
        var lethargy = N(c.Lethargy);
        var vomit = Math.Max(0, c.VomitingCount);
        var diarrhoea = Math.Max(0, c.DiarrhoeaCount);
        var longer = duration is "gt3d" or "morethan3days" or "over3days";
        var midDur = duration is "1to3d" or "13days" or "1to3days";

        // ---- Emergency ----
        if (breathing == "laboured") Raise(TriageUrgency.Emergency, "laboured breathing");
        if (gums is "blue" or "pale" or "white") Raise(TriageUrgency.Emergency, $"{gums} gums");
        if (bleeding == "heavy") Raise(TriageUrgency.Emergency, "heavy bleeding");
        if (c.Seizure) Raise(TriageUrgency.Emergency, "seizure");
        if (c.PossibleToxinExposure) Raise(TriageUrgency.Emergency, "possible poison or toxin exposure");
        if (lethargy == "severe") Raise(TriageUrgency.Emergency, "severe lethargy or collapse");
        if (vomit >= 6) Raise(TriageUrgency.Emergency, $"repeated vomiting ({vomit} times in 24h)");
        var det = TriageDetector.Detect(c.Notes);
        if (det.IsEmergency) Raise(TriageUrgency.Emergency, $"notes mention an emergency sign ({det.Category})");

        // ---- Urgent ----
        if (gums == "yellow") Raise(TriageUrgency.Urgent, "yellow gums");
        if (breathing == "fast") Raise(TriageUrgency.Urgent, "fast breathing");
        if (vomit >= 3) Raise(TriageUrgency.Urgent, $"vomiting {vomit} times in 24h");
        if (diarrhoea >= 6) Raise(TriageUrgency.Urgent, $"frequent diarrhoea ({diarrhoea} times in 24h)");
        if (appetite == "none" && (midDur || longer)) Raise(TriageUrgency.Urgent, "not eating for more than a day");
        if (lethargy == "mild" && appetite == "none") Raise(TriageUrgency.Urgent, "lethargic and not eating");
        if (bleeding == "minor" && longer) Raise(TriageUrgency.Urgent, "bleeding for more than 3 days");
        if (c.YoungOrSenior && (vomit >= 2 || diarrhoea >= 3 || appetite == "none"))
            Raise(TriageUrgency.Urgent, "very young or senior animal with digestive upset or not eating");

        // ---- Soon ----
        if (vomit >= 1) Raise(TriageUrgency.Soon, $"vomiting ({vomit} time{(vomit == 1 ? "" : "s")})");
        if (diarrhoea >= 2) Raise(TriageUrgency.Soon, $"diarrhoea ({diarrhoea} times)");
        if (appetite is "reduced" or "none") Raise(TriageUrgency.Soon, "reduced appetite");
        if (lethargy == "mild") Raise(TriageUrgency.Soon, "mild lethargy");
        if (bleeding == "minor") Raise(TriageUrgency.Soon, "minor bleeding");
        if (longer && (vomit > 0 || diarrhoea > 0 || appetite != "normal" && appetite != "" || lethargy is "mild" or "severe"))
            Raise(TriageUrgency.Soon, "symptoms lasting more than 3 days");

        var urgency = level switch { 3 => TriageUrgency.Emergency, 2 => TriageUrgency.Urgent, 1 => TriageUrgency.Soon, _ => TriageUrgency.Routine };
        if (reasons.Count == 0) reasons.Add("No warning signs reported in the checklist.");

        var species = string.IsNullOrWhiteSpace(c.Species) ? "pet" : c.Species.Trim();
        var summary = urgency switch
        {
            TriageUrgency.Emergency => $"Possible emergency for your {species}: contact a vet immediately.",
            TriageUrgency.Urgent => $"Your {species} should see a vet today.",
            TriageUrgency.Soon => $"Book a vet visit within the next day or two for your {species}.",
            _ => $"No urgent signs reported for your {species}; monitor and book a routine check if it persists.",
        };
        if (hasPhotos) summary += " " + PhotoNotice;
        else summary += " This checklist is not a diagnosis; a vet can review it.";

        return new TriageAssessment(urgency, reasons, true, summary);
    }
}
