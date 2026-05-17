using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Events;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Vet;

public class VetDoctorService : IVetDoctorService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public VetDoctorService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
    }

    private bool IsAdmin =>
        _current.Permissions.Contains(Permissions.Vets.Approve)
        || _current.Permissions.Contains(Permissions.Vets.Reject);

    public async Task<CursorPage<DoctorSummaryDto>> SearchAsync(DoctorSearchInput input, CancellationToken ct = default)
    {
        var q = _db.Doctors.AsNoTracking().Where(d => d.ApprovalStatus == ApprovalStatus.Approved);

        if (input.AnimalType.HasValue)
            q = q.Where(d => d.SupportedAnimalTypes.Any(a => a.AnimalType == input.AnimalType));
        if (!string.IsNullOrWhiteSpace(input.Specialty))
            q = q.Where(d => d.Specialty != null && d.Specialty.Contains(input.Specialty));
        if (input.SpecialtyId.HasValue)
            q = q.Where(d => _db.DoctorSpecialties.Any(s => s.DoctorId == d.Id && s.SpecialtyId == input.SpecialtyId));
        if (!string.IsNullOrWhiteSpace(input.City))
            q = q.Where(d => d.City == input.City);
        if (input.ConsultationType.HasValue)
            q = q.Where(d => d.ConsultationType == input.ConsultationType
                          || d.ConsultationType == ConsultationType.Both);
        if (input.MaxPrice.HasValue)
            q = q.Where(d => d.ConsultationFee <= input.MaxPrice);
        if (input.MinRating.HasValue)
            q = q.Where(d => d.RatingAverage >= input.MinRating);
        if (input.AvailableThisWeek == true)
        {
            var from = DateTime.UtcNow;
            var to   = from.AddDays(7);
            q = q.Where(d => _db.DoctorTimeSlots.Any(s =>
                s.DoctorId == d.Id && s.Status == SlotStatus.Available
                && s.StartUtc >= from && s.StartUtc < to));
        }

        var cur = VetCursor.Decode(input.Cursor);
        if (cur is { } c)
            q = q.Where(d => d.CreatedAt < c.Ts || (d.CreatedAt == c.Ts && d.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(input.PageSize, 1, 50);
        var orderedQ = (input.Sort?.ToLowerInvariant()) switch
        {
            "price"  => q.OrderBy(d => d.ConsultationFee).ThenByDescending(d => d.Id),
            "rating" => q.OrderByDescending(d => d.RatingAverage).ThenByDescending(d => d.Id),
            _        => q.OrderByDescending(d => d.RatingAverage).ThenByDescending(d => d.Id)
        };

        var rows = await orderedQ.Take(take + 1)
            .Select(d => new
            {
                d.Id, d.UserId, Name = d.User.DisplayName, d.User.AvatarUrl,
                d.Specialty, d.ClinicName, d.City, d.Country,
                d.ConsultationFee, d.ConsultationType, d.OnlineAvailable, d.OfflineAvailable,
                d.RatingAverage, d.RatingCount, d.ApprovalStatus, d.CreatedAt,
                SupportedAnimalTypes = d.SupportedAnimalTypes.Select(a => a.AnimalType).ToList(),
                Specialties = _db.DoctorSpecialties.Where(s => s.DoctorId == d.Id).Select(s => s.Specialty.Name).ToList()
            })
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = VetCursor.Encode(last.CreatedAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }

        return new CursorPage<DoctorSummaryDto>(rows.Select(d => new DoctorSummaryDto(
            d.Id, d.UserId, d.Name, d.AvatarUrl, d.Specialty, d.Specialties,
            d.ClinicName, d.City, d.Country, d.ConsultationFee, d.ConsultationType,
            d.OnlineAvailable, d.OfflineAvailable, d.RatingAverage, d.RatingCount,
            d.ApprovalStatus, d.SupportedAnimalTypes)).ToList(), next);
    }

    public async Task<DoctorDetailDto?> GetAsync(Guid doctorId, CancellationToken ct = default)
        => await _db.Doctors.AsNoTracking().Where(d => d.Id == doctorId)
            .Select(d => new DoctorDetailDto(
                d.Id, d.UserId, d.User.DisplayName, d.User.AvatarUrl,
                d.LicenseNumber, d.Specialty, d.ExperienceYears, d.About,
                d.ClinicName, d.ClinicAddress, d.City, d.Country,
                d.ConsultationFee, d.ConsultationType, d.OnlineAvailable, d.OfflineAvailable,
                d.AutoConfirmAppointments, d.DefaultSlotMinutes, d.CancellationCutoffHours,
                d.ApprovalStatus, d.AdminNotes,
                d.RatingAverage, d.RatingCount,
                d.SupportedAnimalTypes.Select(a => a.AnimalType).ToList(),
                _db.DoctorSpecialties.Where(s => s.DoctorId == d.Id)
                    .Select(s => new SpecialtyDto(s.SpecialtyId, s.Specialty.Slug, s.Specialty.Name)).ToList()))
            .SingleOrDefaultAsync(ct);

    public async Task<DoctorDetailDto?> GetMyProfileAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var id = await _db.Doctors.AsNoTracking().Where(d => d.UserId == uid).Select(d => d.Id).SingleOrDefaultAsync(ct);
        return id == Guid.Empty ? null : await GetAsync(id, ct);
    }

    public async Task<Guid> RegisterAsync(RegisterDoctorInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (await _db.Doctors.AnyAsync(d => d.UserId == uid, ct))
            throw new ConflictException("Doctor profile already exists.");

        var doctor = new Doctor
        {
            UserId = uid,
            LicenseNumber = input.LicenseNumber,
            Specialty = input.Specialty,
            ExperienceYears = input.ExperienceYears,
            About = input.About,
            ClinicName = input.ClinicName,
            ClinicAddress = input.ClinicAddress,
            City = input.City,
            Country = input.Country,
            ConsultationFee = input.ConsultationFee,
            ConsultationType = input.ConsultationType,
            OnlineAvailable = input.OnlineAvailable,
            OfflineAvailable = input.OfflineAvailable,
            AutoConfirmAppointments = input.AutoConfirmAppointments,
            DefaultSlotMinutes = input.DefaultSlotMinutes,
            CancellationCutoffHours = input.CancellationCutoffHours,
            ApprovalStatus = ApprovalStatus.Pending
        };
        foreach (var at in input.SupportedAnimalTypes.Distinct())
            doctor.SupportedAnimalTypes.Add(new DoctorAnimalType { AnimalType = at });
        foreach (var sid in input.SpecialtyIds.Distinct())
            _db.DoctorSpecialties.Add(new DoctorSpecialty { Doctor = doctor, SpecialtyId = sid });

        _db.Doctors.Add(doctor);
        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            EntityType = ApprovalEntityType.Doctor,
            EntityId = doctor.Id,
            SubmittedById = uid,
            Decision = ApprovalDecision.Pending,
            SlaDueAt = DateTime.UtcNow.AddHours(72)
        });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Doctors,
            new DoctorRegistered(doctor.Id, uid, DateTime.UtcNow), doctor.Id.ToString(), ct);
        return doctor.Id;
    }

    public async Task UpdateAsync(UpdateDoctorInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var doctor = await _db.Doctors.Include(d => d.SupportedAnimalTypes)
            .SingleOrDefaultAsync(d => d.UserId == uid, ct)
            ?? throw new NotFoundException("Doctor", uid);

        doctor.Specialty = input.Specialty;
        doctor.ExperienceYears = input.ExperienceYears;
        doctor.About = input.About;
        doctor.ClinicName = input.ClinicName;
        doctor.ClinicAddress = input.ClinicAddress;
        doctor.City = input.City;
        doctor.Country = input.Country;
        doctor.ConsultationFee = input.ConsultationFee;
        doctor.ConsultationType = input.ConsultationType;
        doctor.OnlineAvailable = input.OnlineAvailable;
        doctor.OfflineAvailable = input.OfflineAvailable;
        doctor.AutoConfirmAppointments = input.AutoConfirmAppointments;
        doctor.DefaultSlotMinutes = input.DefaultSlotMinutes;
        doctor.CancellationCutoffHours = input.CancellationCutoffHours;

        doctor.SupportedAnimalTypes.Clear();
        foreach (var at in input.SupportedAnimalTypes.Distinct())
            doctor.SupportedAnimalTypes.Add(new DoctorAnimalType { DoctorId = doctor.Id, AnimalType = at });

        _db.DoctorSpecialties.RemoveRange(_db.DoctorSpecialties.Where(s => s.DoctorId == doctor.Id));
        foreach (var sid in input.SpecialtyIds.Distinct())
            _db.DoctorSpecialties.Add(new DoctorSpecialty { DoctorId = doctor.Id, SpecialtyId = sid });

        await _db.SaveChangesAsync(ct);
    }

    // ---------- Credentials ----------

    public async Task<Guid> AddCredentialAsync(AddCredentialInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var doctor = await _db.Doctors.SingleOrDefaultAsync(d => d.UserId == uid, ct)
            ?? throw new NotFoundException("Doctor", uid);

        var doc = new DoctorCredentialDocument
        {
            DoctorId = doctor.Id,
            Kind = (CredentialKind)input.Kind,
            Title = input.Title,
            FileUrl = input.FileUrl,
            IssuingAuthority = input.IssuingAuthority,
            DocumentNumber = input.DocumentNumber,
            IssuedOn = input.IssuedOn,
            ExpiresOn = input.ExpiresOn
        };
        _db.DoctorCredentialDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    public async Task<IReadOnlyList<CredentialDocumentDto>> ListCredentialsAsync(Guid? doctorId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        Guid did;
        if (doctorId.HasValue)
        {
            if (!IsAdmin && !await _db.Doctors.AnyAsync(d => d.Id == doctorId && d.UserId == uid, ct))
                throw new ForbiddenException();
            did = doctorId.Value;
        }
        else
        {
            did = await _db.Doctors.Where(d => d.UserId == uid).Select(d => d.Id).SingleOrDefaultAsync(ct);
            if (did == Guid.Empty) throw new NotFoundException("Doctor", uid);
        }

        return await _db.DoctorCredentialDocuments.AsNoTracking()
            .Where(c => c.DoctorId == did)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CredentialDocumentDto(c.Id, (int)c.Kind, c.Title, c.FileUrl,
                c.IssuingAuthority, c.DocumentNumber, c.IssuedOn, c.ExpiresOn, c.Verified, c.VerifiedAt))
            .ToListAsync(ct);
    }

    public async Task VerifyCredentialAsync(Guid credentialId, bool verified, CancellationToken ct = default)
    {
        if (!IsAdmin) throw new ForbiddenException();
        var uid = _current.UserId ?? throw new ForbiddenException();
        var c = await _db.DoctorCredentialDocuments.SingleOrDefaultAsync(x => x.Id == credentialId, ct)
            ?? throw new NotFoundException("CredentialDocument", credentialId);
        c.Verified = verified;
        c.VerifiedAt = verified ? DateTime.UtcNow : null;
        c.VerifiedByUserId = verified ? uid : null;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(VetTopics.Doctors,
            new CredentialVerified(c.Id, c.DoctorId, uid, verified, DateTime.UtcNow),
            c.Id.ToString(), ct);
    }

    // ---------- Admin ----------

    public Task ApproveAsync(Guid doctorId, string? adminNotes, CancellationToken ct = default)
        => DecideAsync(doctorId, ApprovalStatus.Approved, adminNotes, ct);

    public Task RejectAsync(Guid doctorId, string? adminNotes, CancellationToken ct = default)
        => DecideAsync(doctorId, ApprovalStatus.Rejected, adminNotes, ct);

    public Task SuspendAsync(Guid doctorId, string? reason, CancellationToken ct = default)
        => DecideAsync(doctorId, ApprovalStatus.Suspended, reason, ct);

    private async Task DecideAsync(Guid doctorId, ApprovalStatus status, string? notes, CancellationToken ct)
    {
        if (!IsAdmin) throw new ForbiddenException();
        var uid = _current.UserId ?? throw new ForbiddenException();
        var doctor = await _db.Doctors.SingleOrDefaultAsync(d => d.Id == doctorId, ct)
            ?? throw new NotFoundException("Doctor", doctorId);

        doctor.ApprovalStatus = status;
        doctor.AdminNotes = notes;

        var pending = await _db.ApprovalRequests.FirstOrDefaultAsync(
            a => a.EntityType == ApprovalEntityType.Doctor && a.EntityId == doctorId
              && a.Decision == ApprovalDecision.Pending, ct);
        if (pending is not null)
        {
            pending.Decision = status == ApprovalStatus.Approved
                ? ApprovalDecision.Approved
                : ApprovalDecision.Rejected;
            pending.DecidedById = uid;
            pending.DecidedAt = DateTime.UtcNow;
            pending.AdminNotes = notes;
        }

        _db.Notifications.Add(new InAppNotification
        {
            UserId = doctor.UserId,
            Title = status switch
            {
                ApprovalStatus.Approved  => "You're approved — your vet profile is live 🎉",
                ApprovalStatus.Rejected  => "Your vet application was rejected",
                ApprovalStatus.Suspended => "Your vet profile has been suspended",
                _ => "Vet profile status updated"
            },
            Body = notes ?? "—",
            Url = "/dashboard/vet"
        });

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(status.ToString().ToLowerInvariant(), "Doctor", doctorId.ToString(), "Vet",
            newValues: new { notes }, ct: ct);

        await _kafka.PublishAsync(VetTopics.Doctors, status switch
        {
            ApprovalStatus.Approved  => new DoctorApproved(doctorId, uid, DateTime.UtcNow) as object,
            ApprovalStatus.Rejected  => new DoctorRejected(doctorId, uid, notes, DateTime.UtcNow),
            ApprovalStatus.Suspended => new DoctorSuspended(doctorId, uid, notes, DateTime.UtcNow),
            _ => new { doctorId, status = status.ToString() }
        }, doctorId.ToString(), ct);
    }

    public async Task<IReadOnlyList<CommissionReportRow>> CommissionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Reports.View)) throw new ForbiddenException();
        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        const decimal CommissionPct = 0.10m; // future: pull from system settings

        return await _db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Completed
                     && a.PaymentStatus == PaymentStatus.Paid
                     && a.CompletedAt >= fromUtc && a.CompletedAt <= toUtc)
            .GroupBy(a => new { a.DoctorId, Name = a.Doctor.User.DisplayName })
            .Select(g => new CommissionReportRow(
                g.Key.DoctorId, g.Key.Name,
                g.Sum(x => x.Amount),
                g.Sum(x => x.Amount) * CommissionPct,
                g.Count()))
            .OrderByDescending(r => r.GrossFees)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SpecialtyDto>> ListSpecialtiesAsync(CancellationToken ct = default)
        => await _db.Specialties.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SpecialtyDto(s.Id, s.Slug, s.Name))
            .ToListAsync(ct);
}
