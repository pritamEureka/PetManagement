using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class PetInsightService : IPetInsightService
{
    public const string InsightDisclaimer = "Automated observations from the records you entered. They are not a diagnosis; talk to your vet if you are concerned.";
    private readonly ApplicationDbContext _db;
    private readonly IPetAccessService _access;

    public PetInsightService(ApplicationDbContext db, IPetAccessService access) { _db = db; _access = access; }

    public async Task<PetInsightsDto> GetAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var now = DateTime.UtcNow;
        var insights = new List<PetInsightDto>();
        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId).Select(p => new { p.AnimalType, p.Size }).SingleAsync(ct);

        // Missed doses, last 14 days.
        var from = now.Date.AddDays(-14);
        var meds = await _db.PetMedications.AsNoTracking().Where(m => m.PetId == petId && m.StartDate <= now && (m.EndDate == null || m.EndDate >= from))
            .Select(m => new { m.Id, m.DrugName, m.DoseTimes, m.StartDate, m.EndDate }).ToListAsync(ct);
        foreach (var m in meds)
        {
            var perDay = m.DoseTimes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
            var expected = InsightMath.ExpectedDoses(m.StartDate, m.EndDate, from, now.Date, perDay);
            if (expected < 4) continue;
            var taken = await _db.MedicationDoseLogs.CountAsync(l => l.MedicationId == m.Id && !l.Skipped && l.ScheduledFor >= from && l.ScheduledFor < now.Date, ct);
            var rate = InsightMath.MissedRate(expected, taken);
            if (rate is >= 0.2)
                insights.Add(new PetInsightDto("missed-doses", rate >= 0.5 ? InsightSeverity.Alert : InsightSeverity.Watch,
                    $"{m.DrugName}: {rate:P0} of doses missed",
                    $"{expected - taken} of {expected} expected doses in the last 14 days were not logged. Missing doses can reduce effectiveness; ask your vet before changing the schedule.",
                    $"{rate:P0}"));
        }

        // Weight anomaly.
        var weights = await _db.PetWeightLogs.AsNoTracking().Where(w => w.PetId == petId).OrderBy(w => w.RecordedOn)
            .Select(w => new WeightPoint(w.RecordedOn, w.WeightKg)).ToListAsync(ct);
        var range = PetKnowledge.HealthyRange(pet.AnimalType, pet.Size);
        var wa = InsightMath.DetectWeightAnomaly(weights, range?.MinKg, range?.MaxKg);
        if (wa.IsAnomaly)
            insights.Add(new PetInsightDto("weight-anomaly", InsightSeverity.Watch, "Unusual weight change", wa.Reason!,
                wa.PercentChange is { } pc ? $"{pc:+0.#;-0.#}%" : null));

        // Repeated visits.
        var visits = await _db.MedicalRecords.AsNoTracking().Where(m => m.PetId == petId && m.VisitDate >= now.AddDays(-90))
            .Select(m => new { m.VisitDate, m.Diagnosis }).ToListAsync(ct);
        foreach (var r in InsightMath.RepeatedVisits(visits.Select(v => (v.VisitDate, v.Diagnosis)), now).Take(3))
            insights.Add(new PetInsightDto("repeated-visits", InsightSeverity.Watch, $"Repeated visits for \"{r.Keyword}\"",
                $"{r.Visits} vet visits mentioning \"{r.Keyword}\" in the last 90 days. Consider asking your vet about a longer-term plan.", $"{r.Visits} visits"));

        return new PetInsightsDto(petId, now, insights, InsightDisclaimer);
    }
}
