using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Vet;

public class DoctorAvailabilityService : IDoctorAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public DoctorAvailabilityService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private async Task<Doctor> RequireOwnDoctorAsync(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.Doctors.SingleOrDefaultAsync(d => d.UserId == uid, ct)
            ?? throw new NotFoundException("Doctor", uid);
    }

    // ---------- Rules ----------

    public async Task<IReadOnlyList<AvailabilityRuleDto>> ListRulesAsync(Guid doctorId, CancellationToken ct = default)
        => await _db.DoctorAvailabilities.AsNoTracking()
            .Where(r => r.DoctorId == doctorId)
            .OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTime)
            .Select(r => new AvailabilityRuleDto(r.Id, r.DayOfWeek, r.StartTime, r.EndTime, r.SlotMinutes, r.ConsultationType))
            .ToListAsync(ct);

    public async Task<Guid> AddRuleAsync(AvailabilityRuleInput input, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        if (input.EndTime <= input.StartTime) throw new AppException("invalid_range", "End time must be after start time.");
        if (input.SlotMinutes < 5 || input.SlotMinutes > 240)
            throw new AppException("invalid_slot", "Slot duration must be 5–240 minutes.");

        var rule = new DoctorAvailability
        {
            DoctorId = doctor.Id,
            DayOfWeek = input.DayOfWeek,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            SlotMinutes = input.SlotMinutes,
            ConsultationType = input.ConsultationType
        };
        _db.DoctorAvailabilities.Add(rule);
        await _db.SaveChangesAsync(ct);
        return rule.Id;
    }

    public async Task RemoveRuleAsync(Guid ruleId, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        var rule = await _db.DoctorAvailabilities.SingleOrDefaultAsync(r => r.Id == ruleId && r.DoctorId == doctor.Id, ct)
            ?? throw new NotFoundException("AvailabilityRule", ruleId);
        _db.DoctorAvailabilities.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Holidays ----------

    public async Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(Guid doctorId, CancellationToken ct = default)
        => await _db.DoctorHolidays.AsNoTracking()
            .Where(h => h.DoctorId == doctorId && h.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)))
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Reason))
            .ToListAsync(ct);

    public async Task<Guid> AddHolidayAsync(HolidayInput input, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        if (await _db.DoctorHolidays.AnyAsync(h => h.DoctorId == doctor.Id && h.Date == input.Date, ct))
            return (await _db.DoctorHolidays.Where(h => h.DoctorId == doctor.Id && h.Date == input.Date).Select(h => h.Id).SingleAsync(ct));
        var h = new DoctorHoliday { DoctorId = doctor.Id, Date = input.Date, Reason = input.Reason };
        _db.DoctorHolidays.Add(h);

        // Also blocks any auto-generated slots that overlap.
        var dayStart = input.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd   = input.Date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var slots = _db.DoctorTimeSlots.Where(s => s.DoctorId == doctor.Id
                                                && s.StartUtc >= dayStart && s.StartUtc <= dayEnd
                                                && s.Status == SlotStatus.Available);
        await foreach (var s in slots.AsAsyncEnumerable().WithCancellation(ct))
            s.Status = SlotStatus.Blocked;

        await _db.SaveChangesAsync(ct);
        return h.Id;
    }

    public async Task RemoveHolidayAsync(Guid holidayId, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        var h = await _db.DoctorHolidays.SingleOrDefaultAsync(x => x.Id == holidayId && x.DoctorId == doctor.Id, ct);
        if (h is null) return;
        _db.DoctorHolidays.Remove(h);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Slot generation ----------

    public async Task<int> GenerateSlotsAsync(GenerateSlotsInput input, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        if (input.ToDate < input.FromDate)
            throw new AppException("invalid_range", "ToDate must be on or after FromDate.");
        if ((input.ToDate.DayNumber - input.FromDate.DayNumber) > 90)
            throw new AppException("invalid_range", "Generate up to 90 days at a time.");

        var rules = await _db.DoctorAvailabilities.AsNoTracking()
            .Where(r => r.DoctorId == doctor.Id).ToListAsync(ct);
        var holidays = (await _db.DoctorHolidays.AsNoTracking()
            .Where(h => h.DoctorId == doctor.Id && h.Date >= input.FromDate && h.Date <= input.ToDate)
            .Select(h => h.Date).ToListAsync(ct)).ToHashSet();

        var existing = (await _db.DoctorTimeSlots.AsNoTracking()
            .Where(s => s.DoctorId == doctor.Id
                     && s.StartUtc >= input.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                     && s.StartUtc <= input.ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc))
            .Select(s => s.StartUtc).ToListAsync(ct)).ToHashSet();

        int generated = 0;
        for (var d = input.FromDate; d <= input.ToDate; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) continue;
            var dayRules = rules.Where(r => r.DayOfWeek == d.DayOfWeek);
            foreach (var rule in dayRules)
            {
                for (var t = rule.StartTime; t < rule.EndTime; t = t.AddMinutes(rule.SlotMinutes))
                {
                    var start = d.ToDateTime(t, DateTimeKind.Utc);
                    var end   = start.AddMinutes(rule.SlotMinutes);
                    if (end > d.ToDateTime(rule.EndTime, DateTimeKind.Utc)) break;
                    if (existing.Contains(start)) continue;

                    _db.DoctorTimeSlots.Add(new DoctorTimeSlot
                    {
                        DoctorId = doctor.Id,
                        StartUtc = start,
                        EndUtc = end,
                        ConsultationType = rule.ConsultationType,
                        Status = SlotStatus.Available
                    });
                    generated++;
                }
            }
        }
        await _db.SaveChangesAsync(ct);
        return generated;
    }

    public async Task<IReadOnlyList<TimeSlotDto>> ListSlotsAsync(Guid doctorId, DateOnly from, DateOnly to,
        ConsultationType? type = null, bool availableOnly = true, CancellationToken ct = default)
    {
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc   = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var q = _db.DoctorTimeSlots.AsNoTracking()
            .Where(s => s.DoctorId == doctorId && s.StartUtc >= fromUtc && s.StartUtc <= toUtc);
        if (type.HasValue) q = q.Where(s => s.ConsultationType == type);
        if (availableOnly) q = q.Where(s => s.Status == SlotStatus.Available);

        return await q.OrderBy(s => s.StartUtc)
            .Select(s => new TimeSlotDto(s.Id, s.StartUtc, s.EndUtc, s.ConsultationType, s.Status.ToString()))
            .ToListAsync(ct);
    }

    public async Task BlockSlotAsync(Guid slotId, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        var slot = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctor.Id, ct)
            ?? throw new NotFoundException("Slot", slotId);
        if (slot.Status == SlotStatus.Booked) throw new ConflictException("Cannot block a booked slot.");
        slot.Status = SlotStatus.Blocked;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnblockSlotAsync(Guid slotId, CancellationToken ct = default)
    {
        var doctor = await RequireOwnDoctorAsync(ct);
        var slot = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == slotId && s.DoctorId == doctor.Id, ct)
            ?? throw new NotFoundException("Slot", slotId);
        if (slot.Status != SlotStatus.Blocked) return;
        slot.Status = SlotStatus.Available;
        await _db.SaveChangesAsync(ct);
    }
}
