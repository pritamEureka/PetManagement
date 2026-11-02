using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class PetCareService : IPetCareService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IAuditLogger _audit;

    public PetCareService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _access = access;
        _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // ---------- Weight ----------

    public async Task<WeightHistoryDto> ListWeightsAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId)
            .Select(p => new { p.AnimalType, p.Size }).SingleAsync(ct);
        var rows = await _db.PetWeightLogs.AsNoTracking().Where(w => w.PetId == petId)
            .OrderBy(w => w.RecordedOn)
            .Select(w => new WeightLogDto(w.Id, w.RecordedOn, w.WeightKg, w.Notes)).ToListAsync(ct);
        return new WeightHistoryDto(rows, PetKnowledge.HealthyRange(pet.AnimalType, pet.Size));
    }

    public async Task<WeightLogDto> AddWeightAsync(Guid petId, WeightLogInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanLogCareAsync(petId, ct);
        var log = new PetWeightLog { PetId = petId, RecordedOn = Utc(input.RecordedOn), WeightKg = input.WeightKg, Notes = Clean(input.Notes) };
        _db.PetWeightLogs.Add(log);

        // Keep Pet.WeightKg mirroring the most recent measurement.
        var latest = await _db.PetWeightLogs.Where(w => w.PetId == petId)
            .OrderByDescending(w => w.RecordedOn).Select(w => (DateTime?)w.RecordedOn).FirstOrDefaultAsync(ct);
        if (latest is null || log.RecordedOn >= latest)
        {
            var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
            pet.WeightKg = input.WeightKg;
        }
        await _db.SaveChangesAsync(ct);
        return new WeightLogDto(log.Id, log.RecordedOn, log.WeightKg, log.Notes);
    }

    public async Task DeleteWeightAsync(Guid petId, Guid logId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var log = await _db.PetWeightLogs.SingleOrDefaultAsync(w => w.Id == logId && w.PetId == petId, ct)
            ?? throw new NotFoundException("WeightLog", logId);
        _db.PetWeightLogs.Remove(log);
        await _db.SaveChangesAsync(ct);

        var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
        pet.WeightKg = await _db.PetWeightLogs.Where(w => w.PetId == petId)
            .OrderByDescending(w => w.RecordedOn).Select(w => (decimal?)w.WeightKg).FirstOrDefaultAsync(ct);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Medications ----------

    public async Task<IReadOnlyList<MedicationDto>> ListMedicationsAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        return await _db.PetMedications.AsNoTracking().Where(m => m.PetId == petId)
            .OrderByDescending(m => m.IsActive).ThenByDescending(m => m.StartDate)
            .Select(m => new MedicationDto(m.Id, m.PetId, m.PrescriptionId, m.DrugName, m.Dose, m.DoseTimes,
                m.StartDate, m.EndDate, m.Instructions, m.IsActive,
                m.Doses.Count(d => !d.Skipped), m.Doses.Count(d => d.Skipped)))
            .ToListAsync(ct);
    }

    public async Task<MedicationDto> AddMedicationAsync(Guid petId, MedicationInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var med = new PetMedication { PetId = petId };
        Apply(med, input);
        _db.PetMedications.Add(med);
        await _db.SaveChangesAsync(ct);
        return new MedicationDto(med.Id, petId, null, med.DrugName, med.Dose, med.DoseTimes, med.StartDate, med.EndDate,
            med.Instructions, med.IsActive, 0, 0);
    }

    public async Task UpdateMedicationAsync(Guid petId, Guid medicationId, MedicationInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var med = await _db.PetMedications.SingleOrDefaultAsync(m => m.Id == medicationId && m.PetId == petId, ct)
            ?? throw new NotFoundException("Medication", medicationId);
        Apply(med, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteMedicationAsync(Guid petId, Guid medicationId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var med = await _db.PetMedications.SingleOrDefaultAsync(m => m.Id == medicationId && m.PetId == petId, ct)
            ?? throw new NotFoundException("Medication", medicationId);
        _db.PetMedications.Remove(med);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(PetMedication m, MedicationInput i)
    {
        m.DrugName = i.DrugName.Trim();
        m.Dose = Clean(i.Dose);
        m.DoseTimes = NormalizeDoseTimes(i.DoseTimes);
        m.StartDate = Utc(i.StartDate.Date);
        m.EndDate = i.EndDate.HasValue ? Utc(i.EndDate.Value.Date) : null;
        m.Instructions = Clean(i.Instructions);
        m.IsActive = i.IsActive;
    }

    internal static string NormalizeDoseTimes(string raw) =>
        string.Join(",", ParseDoseTimes(raw).Select(t => t.ToString("HH:mm")));

    internal static IReadOnlyList<TimeOnly> ParseDoseTimes(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => TimeOnly.TryParse(s, out var t) ? (TimeOnly?)t : null)
            .Where(t => t.HasValue).Select(t => t!.Value).Distinct().OrderBy(t => t).ToList();

    public async Task LogDoseAsync(Guid petId, Guid medicationId, DoseLogInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanLogCareAsync(petId, ct);
        var uid = Uid();
        if (!await _db.PetMedications.AnyAsync(m => m.Id == medicationId && m.PetId == petId, ct))
            throw new NotFoundException("Medication", medicationId);

        var scheduled = Utc(input.ScheduledFor);
        var existing = await _db.MedicationDoseLogs
            .SingleOrDefaultAsync(d => d.MedicationId == medicationId && d.ScheduledFor == scheduled, ct);
        if (existing is null)
        {
            _db.MedicationDoseLogs.Add(new MedicationDoseLog
            {
                MedicationId = medicationId, ScheduledFor = scheduled, LoggedByUserId = uid,
                Skipped = input.Skipped, Notes = Clean(input.Notes)
            });
        }
        else
        {
            existing.Skipped = input.Skipped;
            existing.Notes = Clean(input.Notes);
            existing.LoggedAt = DateTime.UtcNow;
            existing.LoggedByUserId = uid;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TodayDoseDto>> TodayDosesAsync(int tzOffsetMinutes, CancellationToken ct = default)
    {
        var uid = Uid();
        var offset = TimeSpan.FromMinutes(Math.Clamp(tzOffsetMinutes, -14 * 60, 14 * 60));
        var nowUtc = DateTime.UtcNow;
        var localToday = (nowUtc + offset).Date;
        var dayStartUtc = DateTime.SpecifyKind(localToday - offset, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var petIds = await PetAccessService.AccessiblePets(_db, uid, PetAccessLevel.Caretaker).Select(p => p.Id).ToListAsync(ct);
        var meds = await _db.PetMedications.AsNoTracking()
            .Where(m => petIds.Contains(m.PetId) && m.IsActive && m.StartDate < dayEndUtc
                     && (m.EndDate == null || m.EndDate >= localToday))
            .Select(m => new
            {
                m.Id, m.PetId, PetName = m.Pet.Name, m.DrugName, m.Dose, m.DoseTimes,
                Logs = m.Doses.Where(d => d.ScheduledFor >= dayStartUtc && d.ScheduledFor < dayEndUtc)
                    .Select(d => new { d.ScheduledFor, d.Skipped }).ToList()
            }).ToListAsync(ct);

        var result = new List<TodayDoseDto>();
        foreach (var m in meds)
        {
            foreach (var t in ParseDoseTimes(m.DoseTimes))
            {
                var scheduledUtc = DateTime.SpecifyKind(localToday.Add(t.ToTimeSpan()) - offset, DateTimeKind.Utc);
                var log = m.Logs.FirstOrDefault(l => l.ScheduledFor == scheduledUtc);
                var status = log is not null ? (log.Skipped ? "skipped" : "taken")
                    : scheduledUtc <= nowUtc ? "due" : "upcoming";
                result.Add(new TodayDoseDto(m.Id, m.PetId, m.PetName, m.DrugName, m.Dose, scheduledUtc, status));
            }
        }
        return result.OrderBy(r => r.ScheduledFor).ToList();
    }

    // ---------- Caretakers ----------

    public async Task<IReadOnlyList<CaretakerDto>> ListCaretakersAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        return await _db.PetCaretakers.AsNoTracking().Where(c => c.PetId == petId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CaretakerDto(c.Id, c.UserId, c.User.DisplayName, c.User.Email, c.User.AvatarUrl, c.Role, c.ExpiresAt, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CaretakerDto> InviteCaretakerAsync(Guid petId, InviteCaretakerInput input, CancellationToken ct = default)
    {
        await _access.EnsureOwnerAsync(petId, ct);
        var uid = Uid();
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email.ToLower() == email, ct)
            ?? throw new NotFoundException("User", input.Email);
        if (user.Id == uid) throw new ConflictException("You already own this pet.");
        if (!user.IsActive || user.IsSuspended) throw new ConflictException("That account can't be added right now.");
        if (await _db.PetCaretakers.AnyAsync(c => c.PetId == petId && c.UserId == user.Id, ct))
            throw new ConflictException("This person already has access.");
        if (await _db.PetCaretakers.CountAsync(c => c.PetId == petId, ct) >= 20)
            throw new ConflictException("A pet can have at most 20 caretakers.");

        var petName = await _db.Pets.Where(p => p.Id == petId).Select(p => p.Name).SingleAsync(ct);
        var c = new PetCaretaker { PetId = petId, UserId = user.Id, Role = input.Role, InvitedById = uid, ExpiresAt = input.ExpiresAt };
        _db.PetCaretakers.Add(c);
        _db.Notifications.Add(new InAppNotification
        {
            UserId = user.Id,
            Title = $"You've been added as a {RoleLabel(input.Role)} for {petName}",
            Body = "Open the pet to see its care plan and records.",
            Url = $"/pets/{petId}"
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("caretaker_added", "Pet", petId.ToString(), "Pets", newValues: new { user.Id, input.Role }, ct: ct);
        return new CaretakerDto(c.Id, user.Id, user.DisplayName, user.Email, user.AvatarUrl, c.Role, c.ExpiresAt, c.CreatedAt);
    }

    public async Task UpdateCaretakerAsync(Guid petId, Guid caretakerId, UpdateCaretakerInput input, CancellationToken ct = default)
    {
        await _access.EnsureOwnerAsync(petId, ct);
        var c = await _db.PetCaretakers.SingleOrDefaultAsync(x => x.Id == caretakerId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Caretaker", caretakerId);
        c.Role = input.Role;
        c.ExpiresAt = input.ExpiresAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveCaretakerAsync(Guid petId, Guid caretakerId, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = await _db.PetCaretakers.SingleOrDefaultAsync(x => x.Id == caretakerId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Caretaker", caretakerId);
        // Owners remove anyone; caretakers can remove themselves (leave).
        if (c.UserId != uid) await _access.EnsureOwnerAsync(petId, ct);
        _db.PetCaretakers.Remove(c);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("caretaker_removed", "Pet", petId.ToString(), "Pets", oldValues: new { c.UserId, c.Role }, ct: ct);
    }

    private static string RoleLabel(CaretakerRole r) => r switch
    {
        CaretakerRole.CoOwner => "co-owner",
        CaretakerRole.Caretaker => "caretaker",
        _ => "viewer"
    };

    // ---------- Routines & logs ----------

    public async Task<IReadOnlyList<CareRoutineDto>> ListRoutinesAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        return await _db.PetCareRoutines.AsNoTracking().Where(r => r.PetId == petId)
            .OrderBy(r => r.TimeOfDay).ThenBy(r => r.Title)
            .Select(r => new CareRoutineDto(r.Id, r.ActivityType, r.Title, r.TimeOfDay, r.Details, r.IsActive))
            .ToListAsync(ct);
    }

    public async Task<CareRoutineDto> AddRoutineAsync(Guid petId, CareRoutineInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        if (await _db.PetCareRoutines.CountAsync(r => r.PetId == petId, ct) >= 30)
            throw new ConflictException("A pet can have at most 30 routines.");
        var r = new PetCareRoutine { PetId = petId };
        Apply(r, input);
        _db.PetCareRoutines.Add(r);
        await _db.SaveChangesAsync(ct);
        return new CareRoutineDto(r.Id, r.ActivityType, r.Title, r.TimeOfDay, r.Details, r.IsActive);
    }

    public async Task UpdateRoutineAsync(Guid petId, Guid routineId, CareRoutineInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var r = await _db.PetCareRoutines.SingleOrDefaultAsync(x => x.Id == routineId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Routine", routineId);
        Apply(r, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRoutineAsync(Guid petId, Guid routineId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var r = await _db.PetCareRoutines.SingleOrDefaultAsync(x => x.Id == routineId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Routine", routineId);
        _db.PetCareRoutines.Remove(r);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(PetCareRoutine r, CareRoutineInput i)
    {
        r.ActivityType = i.ActivityType;
        r.Title = i.Title.Trim();
        r.TimeOfDay = i.TimeOfDay;
        r.Details = Clean(i.Details);
        r.IsActive = i.IsActive;
    }

    public async Task<CareLogDto> AddCareLogAsync(Guid petId, CareLogInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanLogCareAsync(petId, ct);
        var uid = Uid();
        if (input.RoutineId is { } rid && !await _db.PetCareRoutines.AnyAsync(r => r.Id == rid && r.PetId == petId, ct))
            throw new NotFoundException("Routine", rid);

        var log = new PetCareLog
        {
            PetId = petId, RoutineId = input.RoutineId, ActivityType = input.ActivityType,
            PerformedAt = input.PerformedAt.HasValue ? Utc(input.PerformedAt.Value) : DateTime.UtcNow,
            PerformedByUserId = uid, Notes = Clean(input.Notes)
        };
        _db.PetCareLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        var name = await _db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).SingleAsync(ct);
        return new CareLogDto(log.Id, log.ActivityType, log.RoutineId, log.PerformedAt, uid, name, log.Notes);
    }

    public async Task DeleteCareLogAsync(Guid petId, Guid logId, CancellationToken ct = default)
    {
        var uid = Uid();
        var log = await _db.PetCareLogs.SingleOrDefaultAsync(l => l.Id == logId && l.PetId == petId, ct)
            ?? throw new NotFoundException("CareLog", logId);
        if (log.PerformedByUserId != uid) await _access.EnsureCanEditAsync(petId, ct);
        else await _access.EnsureCanLogCareAsync(petId, ct);
        _db.PetCareLogs.Remove(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CareTodayDto> TodayAsync(Guid petId, int tzOffsetMinutes, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var offset = TimeSpan.FromMinutes(Math.Clamp(tzOffsetMinutes, -14 * 60, 14 * 60));
        var localToday = (DateTime.UtcNow + offset).Date;
        var dayStartUtc = DateTime.SpecifyKind(localToday - offset, DateTimeKind.Utc);
        var windowStart = dayStartUtc.AddDays(-60);

        var routines = await _db.PetCareRoutines.AsNoTracking().Where(r => r.PetId == petId && r.IsActive)
            .OrderBy(r => r.TimeOfDay).ThenBy(r => r.Title).ToListAsync(ct);
        var logs = await _db.PetCareLogs.AsNoTracking()
            .Where(l => l.PetId == petId && l.PerformedAt >= windowStart)
            .OrderByDescending(l => l.PerformedAt)
            .Select(l => new CareLogDto(l.Id, l.ActivityType, l.RoutineId, l.PerformedAt, l.PerformedByUserId, l.PerformedBy.DisplayName, l.Notes))
            .ToListAsync(ct);

        var todays = logs.Where(l => l.PerformedAt >= dayStartUtc).ToList();
        var checklist = routines.Select(r =>
        {
            var done = todays.FirstOrDefault(l => l.RoutineId == r.Id);
            return new CareChecklistItemDto(
                new CareRoutineDto(r.Id, r.ActivityType, r.Title, r.TimeOfDay, r.Details, r.IsActive),
                done is not null, done?.PerformedAt, done?.PerformedByName);
        }).ToList();

        // Streak = consecutive local days (ending today, or yesterday if nothing yet today) with any care logged.
        var daysWithCare = logs.Select(l => (l.PerformedAt + offset).Date).ToHashSet();
        var cursor = daysWithCare.Contains(localToday) ? localToday : localToday.AddDays(-1);
        var streak = 0;
        while (daysWithCare.Contains(cursor)) { streak++; cursor = cursor.AddDays(-1); }

        return new CareTodayDto(checklist, streak, logs.Take(20).ToList());
    }
}
