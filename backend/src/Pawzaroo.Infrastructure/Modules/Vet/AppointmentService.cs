using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Events;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Vet;

public class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly ISlotLockService _slotLock;
    private readonly IUnitOfWork _uow;

    public AppointmentService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        ISlotLockService slotLock, IUnitOfWork uow)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _slotLock = slotLock;
        _uow = uow;
    }

    public async Task<CursorPage<AppointmentSummaryDto>> ListAsync(AppointmentScope scope, string? cursor, int pageSize,
        AppointmentStatus? status = null, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var q = _db.Appointments.AsNoTracking().AsQueryable();
        q = scope switch
        {
            AppointmentScope.Mine          => q.Where(a => a.PatientUserId == uid),
            AppointmentScope.ForMyClinic   => q.Where(a => a.Doctor.UserId == uid),
            AppointmentScope.Admin         => q,
            _ => q
        };
        if (status.HasValue) q = q.Where(a => a.Status == status);

        var cur = VetCursor.Decode(cursor);
        if (cur is { } c)
            q = q.Where(a => a.ScheduledAt < c.Ts || (a.ScheduledAt == c.Ts && a.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(pageSize, 1, 100);
        var rows = await q.OrderByDescending(a => a.ScheduledAt).ThenByDescending(a => a.Id)
            .Take(take + 1)
            .Select(a => new AppointmentSummaryDto(
                a.Id, a.DoctorId, a.Doctor.User.DisplayName,
                a.PatientUserId, a.PatientUser.DisplayName,
                a.PetId, a.Pet != null ? a.Pet.Name : null,
                a.ScheduledAt, a.DurationMinutes, a.Type, a.Status, a.PaymentStatus, a.Amount,
                a.MeetingLink, a.PrescriptionFileUrl, a.CreatedAt))
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = VetCursor.Encode(last.ScheduledAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }
        return new CursorPage<AppointmentSummaryDto>(rows, next);
    }

    public async Task<AppointmentSummaryDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var dto = await _db.Appointments.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new
            {
                a.Id, a.DoctorId, DoctorName = a.Doctor.User.DisplayName,
                a.PatientUserId, PatientName = a.PatientUser.DisplayName,
                DoctorOwner = a.Doctor.UserId,
                a.PetId, PetName = a.Pet != null ? a.Pet.Name : null,
                a.ScheduledAt, a.DurationMinutes, a.Type, a.Status, a.PaymentStatus, a.Amount,
                a.MeetingLink, a.PrescriptionFileUrl, a.CreatedAt
            }).SingleOrDefaultAsync(ct);
        if (dto is null) return null;
        if (dto.PatientUserId != uid && dto.DoctorOwner != uid) throw new ForbiddenException();
        return new AppointmentSummaryDto(dto.Id, dto.DoctorId, dto.DoctorName,
            dto.PatientUserId, dto.PatientName, dto.PetId, dto.PetName,
            dto.ScheduledAt, dto.DurationMinutes, dto.Type, dto.Status, dto.PaymentStatus, dto.Amount,
            dto.MeetingLink, dto.PrescriptionFileUrl, dto.CreatedAt);
    }

    public async Task<AppointmentSummaryDto> BookAsync(BookAppointmentInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();

        await using var releaser = await _slotLock.AcquireAsync(input.DoctorId, input.TimeSlotId, TimeSpan.FromSeconds(5), ct);

        AppointmentSummaryDto? result = null;
        await _uow.ExecuteInTransactionAsync(async (token) =>
        {
            var slot = await _db.DoctorTimeSlots
                .SingleOrDefaultAsync(s => s.Id == input.TimeSlotId && s.DoctorId == input.DoctorId, token)
                ?? throw new NotFoundException("Slot", input.TimeSlotId);
            if (slot.Status != SlotStatus.Available)
                throw new ConflictException("Slot is no longer available.");

            var doctor = await _db.Doctors.SingleAsync(d => d.Id == input.DoctorId, token);
            if (doctor.ApprovalStatus != ApprovalStatus.Approved)
                throw new ForbiddenException("Doctor is not currently accepting bookings.");

            var requiresPayment = doctor.ConsultationFee > 0;
            var startStatus = requiresPayment
                ? AppointmentStatus.PendingPayment
                : (doctor.AutoConfirmAppointments ? AppointmentStatus.Confirmed : AppointmentStatus.PendingConfirmation);

            var appt = new Appointment
            {
                DoctorId = input.DoctorId,
                PatientUserId = uid,
                PetId = input.PetId,
                ScheduledAt = slot.StartUtc,
                DurationMinutes = (int)(slot.EndUtc - slot.StartUtc).TotalMinutes,
                Type = input.Type,
                Symptoms = input.Symptoms,
                Amount = doctor.ConsultationFee,
                PaymentStatus = requiresPayment ? PaymentStatus.Unpaid : PaymentStatus.Paid,
                Status = startStatus,
                TimeSlotId = slot.Id,
                ConfirmedAt = startStatus == AppointmentStatus.Confirmed ? DateTime.UtcNow : null
            };
            _db.Appointments.Add(appt);

            slot.Status = SlotStatus.Booked;
            slot.AppointmentId = appt.Id;

            _db.Notifications.Add(new InAppNotification
            {
                UserId = doctor.UserId,
                Title = "New appointment request",
                Body = $"Scheduled at {appt.ScheduledAt:u}",
                Url = $"/dashboard/vet?appointment={appt.Id}"
            });

            await _kafka.PublishAsync(VetTopics.Appointments,
                new AppointmentBooked(appt.Id, doctor.Id, uid, appt.ScheduledAt, appt.Status.ToString(), DateTime.UtcNow),
                appt.Id.ToString(), token);

            result = new AppointmentSummaryDto(
                appt.Id, doctor.Id, "—", uid, "—",
                appt.PetId, null,
                appt.ScheduledAt, appt.DurationMinutes, appt.Type, appt.Status, appt.PaymentStatus, appt.Amount,
                null, null, DateTime.UtcNow);
        }, ct);

        return result!;
    }

    public async Task RescheduleAsync(Guid appointmentId, RescheduleInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);
        if (appt.PatientUserId != uid && (await _db.Doctors.AnyAsync(d => d.Id == appt.DoctorId && d.UserId == uid, ct)) == false)
            throw new ForbiddenException();
        if (appt.Status is AppointmentStatus.Cancelled or AppointmentStatus.CancelledByDoctor
            or AppointmentStatus.CancelledByUser or AppointmentStatus.Completed
            or AppointmentStatus.NoShow or AppointmentStatus.Refunded)
            throw new ConflictException("Cannot reschedule a closed appointment.");

        await using var releaser = await _slotLock.AcquireAsync(appt.DoctorId, input.NewTimeSlotId, TimeSpan.FromSeconds(5), ct);

        await _uow.ExecuteInTransactionAsync(async (token) =>
        {
            var newSlot = await _db.DoctorTimeSlots
                .SingleOrDefaultAsync(s => s.Id == input.NewTimeSlotId && s.DoctorId == appt.DoctorId, token)
                ?? throw new NotFoundException("Slot", input.NewTimeSlotId);
            if (newSlot.Status != SlotStatus.Available) throw new ConflictException("Slot is no longer available.");

            // Release old slot
            if (appt.TimeSlotId is { } oldId)
            {
                var oldSlot = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == oldId, token);
                if (oldSlot is not null) { oldSlot.Status = SlotStatus.Available; oldSlot.AppointmentId = null; }
            }

            newSlot.Status = SlotStatus.Booked;
            newSlot.AppointmentId = appt.Id;
            appt.TimeSlotId = newSlot.Id;
            appt.ScheduledAt = newSlot.StartUtc;
            appt.DurationMinutes = (int)(newSlot.EndUtc - newSlot.StartUtc).TotalMinutes;
            appt.Status = AppointmentStatus.Rescheduled;

            await _kafka.PublishAsync(VetTopics.Appointments,
                new AppointmentRescheduled(appt.Id, appt.ScheduledAt, DateTime.UtcNow), appt.Id.ToString(), token);
        }, ct);

        _db.Notifications.Add(new InAppNotification
        {
            UserId = appt.PatientUserId,
            Title = "Appointment rescheduled",
            Body = $"New time: {appt.ScheduledAt:u}",
            Url = "/appointments"
        });
        await _db.SaveChangesAsync(ct);
    }

    public Task CancelByUserAsync(Guid id, CancelInput input, CancellationToken ct = default) => CancelInternalAsync(id, input, byDoctor: false, ct);
    public Task CancelByDoctorAsync(Guid id, CancelInput input, CancellationToken ct = default) => CancelInternalAsync(id, input, byDoctor: true, ct);

    private async Task CancelInternalAsync(Guid id, CancelInput input, bool byDoctor, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);

        if (byDoctor)
        {
            if (!await _db.Doctors.AnyAsync(d => d.Id == appt.DoctorId && d.UserId == uid, ct))
                throw new ForbiddenException();
        }
        else
        {
            if (appt.PatientUserId != uid) throw new ForbiddenException();
        }

        if (appt.Status is AppointmentStatus.Cancelled or AppointmentStatus.CancelledByUser
            or AppointmentStatus.CancelledByDoctor or AppointmentStatus.Completed
            or AppointmentStatus.NoShow or AppointmentStatus.Refunded)
            throw new ConflictException("Already closed.");

        appt.Status = byDoctor ? AppointmentStatus.CancelledByDoctor : AppointmentStatus.CancelledByUser;
        appt.CancellationReason = input.Reason;
        if (appt.TimeSlotId is { } slotId)
        {
            var slot = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == slotId, ct);
            if (slot is not null) { slot.Status = SlotStatus.Available; slot.AppointmentId = null; }
        }

        _db.Notifications.Add(new InAppNotification
        {
            UserId = byDoctor ? appt.PatientUserId : (await _db.Doctors.Where(d => d.Id == appt.DoctorId).Select(d => d.UserId).SingleAsync(ct)),
            Title = byDoctor ? "Your appointment was cancelled by the doctor" : "Patient cancelled the appointment",
            Body = input.Reason ?? "—",
            Url = "/appointments"
        });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments,
            new AppointmentCancelled(appt.Id, uid, byDoctor, input.Reason, DateTime.UtcNow),
            appt.Id.ToString(), ct);
    }

    public async Task ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);
        if (!await _db.Doctors.AnyAsync(d => d.Id == appt.DoctorId && d.UserId == uid, ct)) throw new ForbiddenException();
        if (appt.Status is not AppointmentStatus.PendingConfirmation and not AppointmentStatus.Rescheduled)
            throw new ConflictException("Cannot confirm from current status.");
        appt.Status = AppointmentStatus.Confirmed;
        appt.ConfirmedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments, new AppointmentConfirmed(appt.Id, DateTime.UtcNow), appt.Id.ToString(), ct);
    }

    public async Task CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);
        if (!await _db.Doctors.AnyAsync(d => d.Id == appt.DoctorId && d.UserId == uid, ct)) throw new ForbiddenException();
        appt.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments, new AppointmentCompleted(appt.Id, DateTime.UtcNow), appt.Id.ToString(), ct);
    }

    public async Task MarkNoShowAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);
        if (!await _db.Doctors.AnyAsync(d => d.Id == appt.DoctorId && d.UserId == uid, ct)) throw new ForbiddenException();
        appt.Status = AppointmentStatus.NoShow;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments, new AppointmentNoShow(appt.Id, DateTime.UtcNow), appt.Id.ToString(), ct);
    }

    public async Task MarkPaidAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);
        if (appt.PatientUserId != uid) throw new ForbiddenException();
        if (appt.PaymentStatus == PaymentStatus.Paid) return;
        appt.PaymentStatus = PaymentStatus.Paid;

        var doctor = await _db.Doctors.SingleAsync(d => d.Id == appt.DoctorId, ct);
        appt.Status = doctor.AutoConfirmAppointments ? AppointmentStatus.Confirmed : AppointmentStatus.PendingConfirmation;
        if (appt.Status == AppointmentStatus.Confirmed) appt.ConfirmedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments,
            new AppointmentPaid(appt.Id, appt.Amount, DateTime.UtcNow), appt.Id.ToString(), ct);
    }

    public async Task RefundAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Appointment", id);
        if (!_current.Permissions.Contains(Application.Common.Permissions.Permissions.Orders.Refund))
            throw new ForbiddenException();
        appt.Status = AppointmentStatus.Refunded;
        appt.PaymentStatus = PaymentStatus.Refunded;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments,
            new AppointmentRefunded(appt.Id, appt.Amount, DateTime.UtcNow), appt.Id.ToString(), ct);
    }

    public async Task<string> EnsureMeetingLinkAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.Include(a => a.Doctor)
            .SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Appointment", id);
        if (appt.PatientUserId != uid && appt.Doctor.UserId != uid) throw new ForbiddenException();
        if (appt.Type != ConsultationType.Online) throw new AppException("not_online", "This appointment isn't an online consultation.");
        if (!string.IsNullOrWhiteSpace(appt.MeetingLink)) return appt.MeetingLink!;

        // Placeholder — production wires this to Jitsi / Twilio / Daily.
        appt.MeetingLink = $"https://meet.jit.si/pawzaroo-{appt.Id:N}";
        await _db.SaveChangesAsync(ct);
        return appt.MeetingLink!;
    }
}
