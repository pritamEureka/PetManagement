using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Events;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Vet;

public class PrescriptionService : IPrescriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public PrescriptionService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<Guid> UploadAsync(Guid appointmentId, PrescriptionInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.Include(a => a.Doctor)
            .SingleOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);
        if (appt.Doctor.UserId != uid) throw new ForbiddenException();

        var rx = new Prescription
        {
            AppointmentId = appointmentId,
            IssuedById = appt.DoctorId,
            FileUrl = input.FileUrl,
            Notes = input.Notes,
            ItemsJson = input.ItemsJson,
            ValidUntil = input.ValidUntil
        };
        _db.Prescriptions.Add(rx);
        appt.PrescriptionFileUrl = input.FileUrl;

        _db.Notifications.Add(new InAppNotification
        {
            UserId = appt.PatientUserId,
            Title = "Prescription is ready",
            Body = appt.PetId is null ? "Tap to view." : "Your pet's prescription is ready.",
            Url = $"/appointments/{appt.Id}"
        });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Appointments,
            new PrescriptionUploaded(appt.Id, appt.DoctorId, DateTime.UtcNow), appt.Id.ToString(), ct);
        return rx.Id;
    }

    public async Task AddFollowUpAsync(Guid appointmentId, FollowUpInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.Include(a => a.Doctor)
            .SingleOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);
        if (appt.Doctor.UserId != uid) throw new ForbiddenException();
        appt.FollowUpNotes = input.Notes;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<object>> ListForPatientAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.Prescriptions.AsNoTracking()
            .Where(p => p.Appointment.PatientUserId == uid)
            .OrderByDescending(p => p.IssuedAt)
            .Select(p => new
            {
                p.Id, p.AppointmentId, p.IssuedAt, p.FileUrl, p.Notes, p.ItemsJson, p.ValidUntil,
                DoctorName = p.IssuedBy.User.DisplayName,
                PetName = p.Appointment.Pet != null ? p.Appointment.Pet.Name : null
            }).ToListAsync(ct);
        return rows.Cast<object>().ToList();
    }
}

public class DoctorReviewService : IDoctorReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public DoctorReviewService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<Guid> SubmitAsync(Guid appointmentId, ReviewInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var appt = await _db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);
        if (appt.PatientUserId != uid) throw new ForbiddenException();
        if (appt.Status != Domain.Common.AppointmentStatus.Completed)
            throw new ConflictException("You can only review completed appointments.");
        if (input.Rating is < 1 or > 5)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["rating"] = new[] { "1..5" } });

        if (await _db.DoctorReviews.AnyAsync(r => r.AppointmentId == appointmentId && r.UserId == uid, ct))
            throw new ConflictException("Already reviewed.");

        var review = new DoctorReview
        {
            DoctorId = appt.DoctorId,
            UserId = uid,
            AppointmentId = appointmentId,
            Rating = input.Rating,
            Comment = input.Comment
        };
        _db.DoctorReviews.Add(review);

        // Recompute rating average + count.
        var doctor = await _db.Doctors.SingleAsync(d => d.Id == appt.DoctorId, ct);
        var newCount = doctor.RatingCount + 1;
        doctor.RatingAverage = ((doctor.RatingAverage * doctor.RatingCount) + input.Rating) / newCount;
        doctor.RatingCount = newCount;

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Doctors,
            new DoctorReviewed(doctor.Id, uid, input.Rating, DateTime.UtcNow), doctor.Id.ToString(), ct);
        return review.Id;
    }

    public async Task<IReadOnlyList<object>> ListForDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        var rows = await _db.DoctorReviews.AsNoTracking()
            .Where(r => r.DoctorId == doctorId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.Rating, r.Comment, r.CreatedAt, ReviewerName = r.User.DisplayName })
            .ToListAsync(ct);
        return rows.Cast<object>().ToList();
    }
}
