using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>Poison / exposure reports with instant escalation guidance, plus the short-lived emergency health card.</summary>
public class ExposureService : IExposureService
{
    public const string EmergencyVetsLink = "/vets?emergency=true";

    public const string Disclaimer =
        "This is general information, not a diagnosis or veterinary advice, and it is not a substitute for a veterinarian. " +
        "Every animal and exposure is different. When in doubt, contact a veterinarian or an animal poison helpline immediately.";

    private static readonly CommonSubstanceDto[] Substances =
    {
        new("chocolate", "Chocolate / cocoa", "Dark chocolate and baking cocoa are generally more dangerous than milk chocolate."),
        new("xylitol", "Xylitol (sugar-free gum, sweets, some peanut butter)", "Xylitol is considered dangerous to dogs even in small amounts."),
        new("grapes", "Grapes / raisins / sultanas", "Can be harmful to dogs in unpredictable amounts."),
        new("onion-garlic", "Onions / garlic / chives", "Cooked, raw and powdered forms can all be a concern."),
        new("rodenticide", "Rat / mouse poison", "Effects can be delayed by days — never wait for symptoms."),
        new("antifreeze", "Antifreeze / engine coolant", "Even tiny amounts are an emergency, especially for cats."),
        new("lily", "Lilies (cats)", "All parts of true lilies, including pollen and vase water, are a serious concern for cats."),
        new("human-medication", "Human medication (painkillers, antidepressants, ADHD drugs…)", "Bring the packaging and note the strength and number of pills."),
        new("pet-medication", "Pet medication overdose / wrong animal's dose", "Note the product, strength and amount."),
        new("household-cleaner", "Household cleaner / bleach / detergent pod", "Keep the product label handy."),
        new("pesticide", "Pesticide / insecticide / snail bait / flea product for another species", "Note the product name and active ingredient."),
        new("plant", "Unknown or toxic plant", "If possible, take a photo or a cutting of the plant."),
        new("other", "Something else", "Describe it as precisely as you can.")
    };

    private static readonly string[] DoNow =
    {
        "Call a veterinarian or emergency clinic now — tell them the pet's species, weight, what was eaten or touched, how much and when.",
        "Keep the packaging, label or a sample of the substance with you, and note the time of exposure.",
        "Remove the pet from the source and secure any remaining substance so it can't be reached again.",
        "Keep the pet calm and warm, and bring them to the clinic if the vet says so."
    };

    private static readonly string[] DoNot =
    {
        "Do not make the pet vomit or give salt, milk, oil or home remedies unless a veterinarian specifically tells you to.",
        "Do not wait for symptoms to appear — some poisons act hours or days later.",
        "Do not give human medication to treat symptoms."
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IStepUpAuthService _stepUp;
    private readonly IAuditLogger _audit;

    public ExposureService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access,
        IStepUpAuthService stepUp, IAuditLogger audit)
    {
        _db = db; _current = current; _access = access; _stepUp = stepUp; _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public IReadOnlyList<CommonSubstanceDto> CommonSubstances() => Substances;

    // ---------- Exposure reports ----------

    public async Task<EscalationDto> ReportAsync(Guid petId, ExposureReportInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await _access.EnsureCanLogCareAsync(petId, ct);
        if (string.IsNullOrWhiteSpace(input.Substance))
            throw new ValidationException(new Dictionary<string, string[]> { ["substance"] = new[] { "Tell us what the pet was exposed to." } });

        var now = DateTime.UtcNow;
        var report = new ExposureReport
        {
            PetId = petId, ReportedByUserId = uid, Substance = input.Substance.Trim(),
            AmountEstimate = Clean(input.AmountEstimate), Symptoms = Clean(input.Symptoms),
            OnsetAt = input.OnsetAt.HasValue ? Utc(input.OnsetAt.Value) : null,
            ExposedAt = input.ExposedAt.HasValue ? Utc(input.ExposedAt.Value) : now,
            Status = ExposureStatus.Reported, Escalated = true
        };
        report.Timeline.Add(new ExposureReportEvent { At = now, UserId = uid, Kind = "reported",
            Note = $"Exposure to {report.Substance} reported." });
        report.Timeline.Add(new ExposureReportEvent { At = now, UserId = uid, Kind = "escalated",
            Note = "Emergency veterinary contact advised. Caretakers notified." });
        _db.Set<ExposureReport>().Add(report);

        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId).Select(p => new { p.Name, p.OwnerId }).SingleAsync(ct);
        var recipients = await _db.PetCaretakers.AsNoTracking()
            .Where(c => c.PetId == petId && (c.ExpiresAt == null || c.ExpiresAt > now)).Select(c => c.UserId).ToListAsync(ct);
        foreach (var r in recipients.Prepend(pet.OwnerId).Distinct().Where(r => r != uid))
            _db.Notifications.Add(new InAppNotification
            {
                UserId = r,
                Title = $"Possible poisoning: {pet.Name}",
                Body = $"{pet.Name} may have been exposed to {report.Substance}. Contact an emergency vet now.",
                Url = $"/pets/{petId}"
            });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("exposure_reported", "Pet", petId.ToString(), "Pets", newValues: new { report.Id, report.Substance }, ct: ct);

        var dto = ToDto(report, pet.Name);
        var matched = Substances.FirstOrDefault(s => s.Key != "other" && report.Substance.Contains(s.Key.Split('-')[0], StringComparison.OrdinalIgnoreCase));
        var guidance = "Many common household items, foods and products can poison pets, and the amount that is dangerous depends on the species, size and health of the animal. " +
            "A veterinarian or poison helpline can judge the risk using the substance, amount, your pet's weight and the time since exposure." +
            (matched is null ? "" : $" Note on {matched.Label}: {matched.Note}");
        return new EscalationDto(
            true,
            "Treat this as an emergency",
            $"Call your nearest emergency vet now about {pet.Name}'s exposure to {report.Substance}. Don't wait for symptoms.",
            "Call your nearest emergency vet now",
            EmergencyVetsLink, guidance, Disclaimer, DoNow, DoNot, dto);
    }

    public async Task<IReadOnlyList<ExposureReportDto>> ListAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var name = await _db.Pets.AsNoTracking().Where(p => p.Id == petId).Select(p => p.Name).SingleAsync(ct);
        var rows = await _db.Set<ExposureReport>().AsNoTracking().Include(r => r.Timeline)
            .Where(r => r.PetId == petId).OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return rows.Select(r => ToDto(r, name)).ToList();
    }

    public async Task<ExposureReportDto> UpdateAsync(Guid petId, Guid reportId, ExposureUpdateInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await _access.EnsureCanLogCareAsync(petId, ct);
        var r = await _db.Set<ExposureReport>().Include(x => x.Timeline).SingleOrDefaultAsync(x => x.Id == reportId && x.PetId == petId, ct)
            ?? throw new NotFoundException("ExposureReport", reportId);
        if (input.Status is { } st && st != r.Status)
        {
            r.Status = st;
            r.Timeline.Add(new ExposureReportEvent { UserId = uid, Kind = $"status:{st}", Note = Clean(input.Note) });
        }
        else if (Clean(input.Note) is { } note)
        {
            r.Timeline.Add(new ExposureReportEvent { UserId = uid, Kind = "note", Note = note });
        }
        await _db.SaveChangesAsync(ct);
        var name = await _db.Pets.AsNoTracking().Where(p => p.Id == petId).Select(p => p.Name).SingleAsync(ct);
        return ToDto(r, name);
    }

    private static ExposureReportDto ToDto(ExposureReport r, string petName) => new(
        r.Id, r.PetId, petName, r.Substance, r.AmountEstimate, r.OnsetAt, r.ExposedAt, r.Symptoms, r.Status, r.Escalated, r.CreatedAt,
        r.Timeline.OrderBy(t => t.At).Select(t => new ExposureEventDto(t.At, t.Kind, t.Note)).ToList());

    // ---------- Emergency health card ----------

    public async Task<EmergencyCardDto> GetEmergencyCardAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        return await BuildCardAsync(petId, null, ct);
    }

    public async Task<EmergencyCardLinkDto> CreateEmergencyCardLinkAsync(Guid petId, int validHours, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        // Same re-authentication bar as health share links.
        await _stepUp.RequireRecentAsync(ct);
        var hours = Math.Clamp(validHours <= 0 ? 24 : validHours, 1, 24 * 7);
        var link = new PetEmergencyCardLink
        {
            PetId = petId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('='),
            ExpiresAt = DateTime.UtcNow.AddHours(hours)
        };
        _db.Set<PetEmergencyCardLink>().Add(link);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("emergency_card_link_created", "Pet", petId.ToString(), "Pets", newValues: new { link.Id, link.ExpiresAt }, ct: ct);
        return new EmergencyCardLinkDto(link.Id, link.Token, $"/v1/public/emergency-card/{link.Token}/view", link.ExpiresAt);
    }

    public async Task RevokeEmergencyCardLinksAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var now = DateTime.UtcNow;
        var links = await _db.Set<PetEmergencyCardLink>().Where(l => l.PetId == petId && l.RevokedAt == null).ToListAsync(ct);
        foreach (var l in links) l.RevokedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EmergencyCardDto?> GetPublicEmergencyCardAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return null;
        var now = DateTime.UtcNow;
        var link = await _db.Set<PetEmergencyCardLink>().AsNoTracking()
            .SingleOrDefaultAsync(l => l.Token == token && l.RevokedAt == null && l.ExpiresAt > now, ct);
        return link is null ? null : await BuildCardAsync(link.PetId, link.ExpiresAt, ct);
    }

    private async Task<EmergencyCardDto> BuildCardAsync(Guid petId, DateTime? expiresAt, CancellationToken ct)
    {
        var p = await _db.Pets.AsNoTracking().Where(x => x.Id == petId)
            .Select(x => new { x.Name, x.AnimalType, x.Breed, x.WeightKg, x.BirthDate, x.MicrochipNumber, x.Allergies,
                OwnerName = x.Owner.DisplayName, OwnerPhone = x.Owner.PhoneNumber })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Pet", petId);

        var typed = await _db.Set<PetAllergy>().AsNoTracking().Where(a => a.PetId == petId && a.Active)
            .OrderByDescending(a => a.Severity).Select(a => new { a.Allergen, a.Severity, a.Reaction }).ToListAsync(ct);
        var allergies = typed.Select(a => $"{a.Allergen} ({a.Severity}{(string.IsNullOrWhiteSpace(a.Reaction) ? "" : ": " + a.Reaction)})").ToList();
        if (allergies.Count == 0 && !string.IsNullOrWhiteSpace(p.Allergies)) allergies.Add(p.Allergies!);

        var now = DateTime.UtcNow;
        var meds = await _db.PetMedications.AsNoTracking()
            .Where(m => m.PetId == petId && m.IsActive && (m.EndDate == null || m.EndDate >= now.Date))
            .Select(m => new { m.DrugName, m.Dose }).Take(20).ToListAsync(ct);
        var conditions = await _db.MedicalRecords.AsNoTracking()
            .Where(m => m.PetId == petId && m.Diagnosis != null && m.Diagnosis != "")
            .OrderByDescending(m => m.VisitDate).Select(m => m.Diagnosis!).Take(10).ToListAsync(ct);

        var vet = await _db.Appointments.AsNoTracking()
            .Where(a => a.PetId == petId && a.Status != AppointmentStatus.CancelledByUser && a.Status != AppointmentStatus.CancelledByDoctor && a.Status != AppointmentStatus.Draft)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => new { a.Doctor.User.DisplayName, a.Doctor.ClinicName, a.Doctor.User.PhoneNumber })
            .FirstOrDefaultAsync(ct);

        return new EmergencyCardDto(p.Name, p.AnimalType.ToString(), p.Breed, p.WeightKg, p.BirthDate?.ToString("yyyy-MM-dd"), p.MicrochipNumber,
            allergies, meds.Select(m => string.IsNullOrWhiteSpace(m.Dose) ? m.DrugName : $"{m.DrugName} ({m.Dose})").ToList(),
            conditions.Select(c => c.Length > 120 ? c[..120] : c).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList(),
            vet?.DisplayName, vet?.ClinicName, vet?.PhoneNumber, p.OwnerName, p.OwnerPhone, now, expiresAt);
    }
}
