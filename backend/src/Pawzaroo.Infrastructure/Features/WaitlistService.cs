using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.VetPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

public class WaitlistService : IWaitlistService, IWaitlistNotifier
{
    /// <summary>How many waitlisted users are told about one freed slot.</summary>
    private const int NotifyBatch = 3;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public WaitlistService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<WaitlistEntryDto> JoinAsync(JoinWaitlistInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var doctor = await _db.Doctors.AsNoTracking().Include(d => d.User)
            .SingleOrDefaultAsync(d => d.Id == input.DoctorId, ct)
            ?? throw new NotFoundException("Doctor", input.DoctorId);
        if (doctor.ApprovalStatus != ApprovalStatus.Approved)
            throw new ForbiddenException("Doctor is not currently accepting bookings.");
        if (doctor.UserId == uid) throw new AppException("invalid_waitlist", "You cannot join your own waitlist.");
        if (input.PreferredFrom is { } f && input.PreferredTo is { } t && t < f)
            throw new AppException("invalid_range", "Preferred end date must not be before the start date.");

        var exists = await _db.Set<AppointmentWaitlistEntry>()
            .AnyAsync(w => w.UserId == uid && w.DoctorId == input.DoctorId && w.Status == WaitlistStatus.Waiting, ct);
        if (exists) throw new ConflictException("You are already on this doctor's waitlist.");

        var entry = new AppointmentWaitlistEntry
        {
            DoctorId = input.DoctorId,
            UserId = uid,
            PetId = input.PetId,
            PreferredFrom = input.PreferredFrom,
            PreferredTo = input.PreferredTo,
            ConsultationType = input.ConsultationType,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()
        };
        if (entry.Note is { Length: > 500 }) throw new AppException("note_too_long", "Note is too long (max 500 characters).");
        _db.Set<AppointmentWaitlistEntry>().Add(entry);
        await _db.SaveChangesAsync(ct);

        var position = await PositionAsync(entry, ct);
        return ToDto(entry, doctor.User.DisplayName, position);
    }

    private Task<int> PositionAsync(AppointmentWaitlistEntry e, CancellationToken ct) =>
        _db.Set<AppointmentWaitlistEntry>().CountAsync(w =>
            w.DoctorId == e.DoctorId && w.Status == WaitlistStatus.Waiting && w.CreatedAt <= e.CreatedAt, ct);

    public async Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(CancellationToken ct = default)
    {
        var uid = Uid();

        // Lazily mark notified entries as fulfilled once the user booked with that doctor afterwards.
        var notified = await _db.Set<AppointmentWaitlistEntry>()
            .Where(w => w.UserId == uid && w.Status == WaitlistStatus.Notified).ToListAsync(ct);
        var changed = false;
        foreach (var w in notified)
        {
            var since = w.NotifiedAt ?? w.CreatedAt;
            var booked = await _db.Appointments.AnyAsync(a =>
                a.PatientUserId == uid && a.DoctorId == w.DoctorId && a.CreatedAt >= since
                && a.Status != AppointmentStatus.CancelledByUser && a.Status != AppointmentStatus.CancelledByDoctor, ct);
            if (booked) { w.Status = WaitlistStatus.Fulfilled; changed = true; }
        }
        if (changed) await _db.SaveChangesAsync(ct);

        var rows = await _db.Set<AppointmentWaitlistEntry>().AsNoTracking()
            .Include(w => w.Doctor).ThenInclude(d => d.User)
            .Where(w => w.UserId == uid && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        var result = new List<WaitlistEntryDto>(rows.Count);
        foreach (var w in rows)
        {
            var pos = w.Status == WaitlistStatus.Waiting ? await PositionAsync(w, ct) : 0;
            result.Add(ToDto(w, w.Doctor.User.DisplayName, pos));
        }
        return result;
    }

    public async Task LeaveAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var e = await _db.Set<AppointmentWaitlistEntry>().SingleOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Waitlist entry", id);
        if (e.UserId != uid) throw new ForbiddenException();
        e.Status = WaitlistStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
    }

    public async Task OnSlotFreedAsync(Guid doctorId, DateTime slotStartUtc, DateTime slotEndUtc,
        ConsultationType slotType, Guid? excludeUserId, CancellationToken ct = default)
    {
        if (slotStartUtc <= DateTime.UtcNow) return;
        var day = DateOnly.FromDateTime(slotStartUtc);

        var candidates = await _db.Set<AppointmentWaitlistEntry>()
            .Where(w => w.DoctorId == doctorId && w.Status == WaitlistStatus.Waiting
                        && (excludeUserId == null || w.UserId != excludeUserId)
                        && (w.PreferredFrom == null || w.PreferredFrom <= day)
                        && (w.PreferredTo == null || w.PreferredTo >= day))
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);

        // Type filter in memory: Both on either side matches anything.
        var eligible = candidates
            .Where(w => w.ConsultationType is null || slotType == ConsultationType.Both
                        || w.ConsultationType == ConsultationType.Both || w.ConsultationType == slotType)
            .Take(NotifyBatch)
            .ToList();
        if (eligible.Count == 0) return;

        var doctorName = await _db.Doctors.Where(d => d.Id == doctorId)
            .Select(d => d.User.DisplayName).SingleOrDefaultAsync(ct) ?? "your vet";
        foreach (var w in eligible)
        {
            w.Status = WaitlistStatus.Notified;
            w.NotifiedAt = DateTime.UtcNow;
            _db.Notifications.Add(new InAppNotification
            {
                UserId = w.UserId,
                Title = "A slot just opened up",
                Body = $"{doctorName} has an opening at {slotStartUtc:u}. Book it before someone else does.",
                Url = $"/vets/{doctorId}/book"
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static WaitlistEntryDto ToDto(AppointmentWaitlistEntry e, string doctorName, int position) =>
        new(e.Id, e.DoctorId, doctorName, e.Status, position, e.PreferredFrom, e.PreferredTo,
            e.ConsultationType, e.Note, e.CreatedAt, e.NotifiedAt);
}
