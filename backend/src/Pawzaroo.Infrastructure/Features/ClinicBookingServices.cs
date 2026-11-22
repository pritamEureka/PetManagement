using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.ClinicPlus;
using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Application.Modules.Vet.Events;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

// =====================================================================================
// Emergency / 24-7 discovery
// =====================================================================================
public class EmergencyVetService : IEmergencyVetService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public EmergencyVetService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    /// <summary>Keeps digits and a single leading '+', producing a safe tel: URI (or null when it is not a plausible number).</summary>
    public static string? ToTelUri(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var t = phone.Trim();
        var plus = t.StartsWith('+');
        var digits = new string(t.Where(char.IsDigit).ToArray());
        if (digits.Length is < 5 or > 15) return null;
        return "tel:" + (plus ? "+" : "") + digits;
    }

    private async Task<Doctor> MyDoctorAsync(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.Doctors.AsNoTracking().SingleOrDefaultAsync(d => d.UserId == uid, ct)
               ?? throw new ForbiddenException("Only registered veterinarians can manage an emergency profile.");
    }

    private static EmergencyProfileDto ToDto(DoctorEmergencyProfile p) =>
        new(p.Is24x7, p.EmergencyPhone, p.ClinicName, p.ClinicAddress, p.City, p.Latitude, p.Longitude,
            p.AcceptsWalkInEmergency, p.UpdatedAt ?? p.CreatedAt);

    public async Task<EmergencyProfileDto?> GetMineAsync(CancellationToken ct = default)
    {
        var d = await MyDoctorAsync(ct);
        var p = await _db.Set<DoctorEmergencyProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.DoctorId == d.Id, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<EmergencyProfileDto> UpsertMineAsync(UpsertEmergencyProfileInput input, CancellationToken ct = default)
    {
        var d = await MyDoctorAsync(ct);
        var errors = new Dictionary<string, string[]>();
        var phone = input.EmergencyPhone?.Trim();
        if (string.IsNullOrEmpty(phone)) phone = null;
        if (phone is not null && ToTelUri(phone) is null) errors["emergencyPhone"] = new[] { "Enter a valid phone number." };
        if ((input.Latitude is null) != (input.Longitude is null)) errors["latitude"] = new[] { "Provide both latitude and longitude, or neither." };
        else if (input.Latitude is { } la && input.Longitude is { } lo
                 && (la is < -90 or > 90 || lo is < -180 or > 180 || double.IsNaN(la) || double.IsNaN(lo)))
            errors["latitude"] = new[] { "Coordinates are out of range." };
        if ((input.Is24x7 || input.AcceptsWalkInEmergency) && phone is null)
            errors["emergencyPhone"] = new[] { "An emergency phone number is required when you offer 24/7 or walk-in emergency care." };
        if (errors.Count > 0) throw new Shared.Exceptions.ValidationException(errors);

        var set = _db.Set<DoctorEmergencyProfile>();
        var p = await set.SingleOrDefaultAsync(x => x.DoctorId == d.Id, ct);
        if (p is null) { p = new DoctorEmergencyProfile { DoctorId = d.Id }; set.Add(p); }
        p.Is24x7 = input.Is24x7;
        p.EmergencyPhone = phone;
        p.ClinicName = Trim(input.ClinicName, 200) ?? d.ClinicName;
        p.ClinicAddress = Trim(input.ClinicAddress, 400) ?? d.ClinicAddress;
        p.City = Trim(input.City, 120) ?? d.City;
        p.Latitude = input.Latitude;
        p.Longitude = input.Longitude;
        p.AcceptsWalkInEmergency = input.AcceptsWalkInEmergency;
        p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    private static string? Trim(string? s, int max) { s = s?.Trim(); return string.IsNullOrEmpty(s) ? null : (s.Length > max ? s[..max] : s); }

    public async Task<IReadOnlyList<EmergencyVetDto>> SearchAsync(double? lat, double? lng, string? city, AnimalType? animalType,
        int limit = 30, CancellationToken ct = default)
    {
        var q = _db.Set<DoctorEmergencyProfile>().AsNoTracking()
            .Where(p => (p.Is24x7 || p.AcceptsWalkInEmergency) && p.EmergencyPhone != null);
        var doctors = _db.Doctors.AsNoTracking().Where(d => d.ApprovalStatus == ApprovalStatus.Approved);
        if (animalType is { } at) doctors = doctors.Where(d => d.SupportedAnimalTypes.Any(t => t.AnimalType == at));

        var rows = await q.Join(doctors, p => p.DoctorId, d => d.Id, (p, d) => new
        {
            p.DoctorId, Name = d.User.DisplayName, p.ClinicName, p.ClinicAddress, p.City, p.Is24x7, p.AcceptsWalkInEmergency,
            p.EmergencyPhone, p.Latitude, p.Longitude, d.OnlineAvailable
        }).Take(1000).ToListAsync(ct);

        var byId = rows.ToDictionary(r => r.DoctorId);
        var ranked = EmergencyOrdering.Order(
            rows.Select(r => new EmergencyCandidate(r.DoctorId, r.Is24x7, r.AcceptsWalkInEmergency, r.City, r.Latitude, r.Longitude, true)),
            lat, lng, city);

        return ranked.Take(Math.Clamp(limit, 1, 100)).Select(x =>
        {
            var r = byId[x.Candidate.DoctorId];
            return new EmergencyVetDto(r.DoctorId, r.Name, r.ClinicName, r.ClinicAddress, r.City, r.Is24x7,
                r.AcceptsWalkInEmergency, r.EmergencyPhone, ToTelUri(r.EmergencyPhone), x.DistanceKm, x.CityMatch, r.OnlineAvailable);
        }).ToList();
    }
}

// =====================================================================================
// Booking policy, deposits, cancellation fees
// =====================================================================================
public class BookingPolicyService : IBookingPolicyService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public BookingPolicyService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private static BookingPolicyDto ToDto(DoctorBookingPolicy p) =>
        new(p.DepositPercent, p.DepositFixed, p.CancellationWindowHours, p.LateCancelFeePercent, p.NoShowFeePercent,
            p.AllowRescheduleHours, p.RequiresQuestionnaire);

    public async Task<BookingPolicyDto?> GetMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var did = await _db.Doctors.Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
                  ?? throw new ForbiddenException("Only registered veterinarians can manage a booking policy.");
        var p = await _db.Set<DoctorBookingPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.DoctorId == did, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<BookingPolicyDto> UpsertMineAsync(BookingPolicyDto i, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var did = await _db.Doctors.Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
                  ?? throw new ForbiddenException("Only registered veterinarians can manage a booking policy.");
        var errors = new Dictionary<string, string[]>();
        void Pct(string k, decimal? v) { if (v is < 0 or > 100) errors[k] = new[] { "Must be between 0 and 100." }; }
        Pct("depositPercent", i.DepositPercent); Pct("lateCancelFeePercent", i.LateCancelFeePercent); Pct("noShowFeePercent", i.NoShowFeePercent);
        if (i.DepositFixed is < 0) errors["depositFixed"] = new[] { "Must not be negative." };
        if (i.CancellationWindowHours is < 0 or > 24 * 30) errors["cancellationWindowHours"] = new[] { "Must be between 0 and 720." };
        if (i.AllowRescheduleHours is < 0 or > 24 * 30) errors["allowRescheduleHours"] = new[] { "Must be between 0 and 720." };
        if (errors.Count > 0) throw new Shared.Exceptions.ValidationException(errors);

        var set = _db.Set<DoctorBookingPolicy>();
        var p = await set.SingleOrDefaultAsync(x => x.DoctorId == did, ct);
        if (p is null) { p = new DoctorBookingPolicy { DoctorId = did }; set.Add(p); }
        p.DepositPercent = i.DepositPercent is > 0 ? i.DepositPercent : null;
        p.DepositFixed = i.DepositFixed is > 0 ? i.DepositFixed : null;
        p.CancellationWindowHours = i.CancellationWindowHours;
        p.LateCancelFeePercent = i.LateCancelFeePercent;
        p.NoShowFeePercent = i.NoShowFeePercent;
        p.AllowRescheduleHours = i.AllowRescheduleHours;
        p.RequiresQuestionnaire = i.RequiresQuestionnaire;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<BookingPolicyDto?> GetForDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        var p = await _db.Set<DoctorBookingPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.DoctorId == doctorId, ct);
        return p is null ? null : ToDto(p);
    }

    private Task<DoctorBookingPolicy?> PolicyAsync(Guid doctorId, CancellationToken ct) =>
        _db.Set<DoctorBookingPolicy>().AsNoTracking().SingleOrDefaultAsync(x => x.DoctorId == doctorId, ct);

    private async Task<AppointmentDeposit?> DepositAsync(Guid appointmentId, CancellationToken ct)
    {
        var local = _db.Set<AppointmentDeposit>().Local.FirstOrDefault(d => d.AppointmentId == appointmentId);
        return local ?? await _db.Set<AppointmentDeposit>().SingleOrDefaultAsync(d => d.AppointmentId == appointmentId, ct);
    }

    public async Task ApplyOnBookingAsync(Appointment appt, CancellationToken ct = default)
    {
        var p = await PolicyAsync(appt.DoctorId, ct);
        if (p is null) return;
        var due = BookingPolicyMath.DepositDue(appt.Amount, p.DepositPercent, p.DepositFixed);
        if (due <= 0) return;
        _db.Set<AppointmentDeposit>().Add(new AppointmentDeposit { AppointmentId = appt.Id, Amount = due, Status = DepositStatus.Pending });
    }

    public async Task OnPaidAsync(Appointment appt, CancellationToken ct = default)
    {
        var d = await DepositAsync(appt.Id, ct);
        if (d is { Status: DepositStatus.Pending }) d.Status = DepositStatus.Paid;
    }

    private static decimal Paid(AppointmentDeposit? d) => d is { Status: DepositStatus.Paid } ? d.Amount : 0;

    public async Task<CancellationOutcomeDto> ApplyCancellationAsync(Appointment appt, bool byDoctor, bool waivePenalty, CancellationToken ct = default)
    {
        var d = await DepositAsync(appt.Id, ct);
        var p = await PolicyAsync(appt.DoctorId, ct);
        var cutoff = p?.CancellationWindowHours
                     ?? await _db.Doctors.Where(x => x.Id == appt.DoctorId).Select(x => x.CancellationCutoffHours).SingleOrDefaultAsync(ct);
        var r = BookingPolicyMath.Cancellation(appt.ScheduledAt, DateTime.UtcNow, cutoff, p?.LateCancelFeePercent ?? 0,
            appt.Amount, Paid(d), byDoctor || waivePenalty);
        if (d is null || d.Status is DepositStatus.Refunded or DepositStatus.Forfeited)
            return new CancellationOutcomeDto(appt.Id, r.IsLate, r.Fee, 0, 0, r.Fee, d?.Status);

        var wasPaid = d.Status == DepositStatus.Paid;
        d.FeeAmount = r.Fee;
        d.RefundAmount = r.Refund;
        d.ResolvedAt = DateTime.UtcNow;
        if (r.Forfeited > 0) { d.Status = DepositStatus.Forfeited; d.Note = $"Late cancellation fee {r.Fee:0.00}; {r.Forfeited:0.00} forfeited, {r.Refund:0.00} refunded."; }
        else { d.Status = DepositStatus.Refunded; d.Note = wasPaid ? "Deposit refunded." : "Cancelled before the deposit was paid."; }
        if (wasPaid && r.Refund > 0 && r.Forfeited == 0) appt.PaymentStatus = PaymentStatus.Refunded;
        return new CancellationOutcomeDto(appt.Id, r.IsLate, r.Fee, r.Forfeited, r.Refund, r.Outstanding, d.Status);
    }

    public async Task ApplyNoShowAsync(Appointment appt, CancellationToken ct = default)
    {
        var d = await DepositAsync(appt.Id, ct);
        if (d is not { Status: DepositStatus.Paid or DepositStatus.Pending }) return;
        var p = await PolicyAsync(appt.DoctorId, ct);
        var r = BookingPolicyMath.NoShow(p?.NoShowFeePercent ?? 0, appt.Amount, Paid(d));
        d.FeeAmount = r.Fee; d.RefundAmount = r.Refund; d.ResolvedAt = DateTime.UtcNow;
        if (r.Forfeited > 0) { d.Status = DepositStatus.Forfeited; d.Note = $"No-show fee {r.Fee:0.00}."; }
        else if (d.Status == DepositStatus.Paid) { d.Status = DepositStatus.Refunded; d.Note = "No-show; no fee applies, deposit refunded."; }
        else { d.Status = DepositStatus.Refunded; d.Note = "No-show before the deposit was paid."; }
    }

    public async Task EnsureRescheduleAllowedAsync(Appointment appt, CancellationToken ct = default)
    {
        if (_current.UserId != appt.PatientUserId) return; // doctors may always reschedule
        var p = await PolicyAsync(appt.DoctorId, ct);
        if (p is null || p.AllowRescheduleHours <= 0) return;
        if ((appt.ScheduledAt - DateTime.UtcNow).TotalHours < p.AllowRescheduleHours)
            throw new AppException("reschedule_window_closed",
                $"This vet only allows rescheduling at least {p.AllowRescheduleHours} hours before the visit.");
    }

    public async Task<DepositDto?> GetDepositAsync(Guid appointmentId, CancellationToken ct = default)
    {
        await EnsureParticipantAsync(appointmentId, ct);
        var d = await _db.Set<AppointmentDeposit>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
        return d is null ? null : new DepositDto(d.AppointmentId, d.Amount, d.Status, d.FeeAmount, d.RefundAmount, d.Note, d.ResolvedAt);
    }

    private async Task<Appointment> EnsureParticipantAsync(Guid appointmentId, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var a = await _db.Appointments.AsNoTracking().Include(x => x.Doctor).SingleOrDefaultAsync(x => x.Id == appointmentId, ct)
                ?? throw new NotFoundException("Appointment", appointmentId);
        if (a.PatientUserId != uid && a.Doctor.UserId != uid) throw new ForbiddenException();
        return a;
    }

    public async Task<CancellationQuoteDto> QuoteCancellationAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var a = await EnsureParticipantAsync(appointmentId, ct);
        var byDoctor = a.Doctor.UserId == _current.UserId;
        var p = await PolicyAsync(a.DoctorId, ct);
        var d = await _db.Set<AppointmentDeposit>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
        var window = p?.CancellationWindowHours ?? a.Doctor.CancellationCutoffHours;
        var r = BookingPolicyMath.Cancellation(a.ScheduledAt, DateTime.UtcNow, window, p?.LateCancelFeePercent ?? 0, a.Amount, Paid(d), byDoctor);
        return new CancellationQuoteDto(a.Id, r.IsLate, window, p?.LateCancelFeePercent ?? 0, r.Fee, r.Forfeited, r.Refund, r.Outstanding, a.ScheduledAt);
    }

    public async Task<CancellationOutcomeDto?> GetCancellationOutcomeAsync(Guid appointmentId, CancellationToken ct = default)
    {
        await EnsureParticipantAsync(appointmentId, ct);
        var d = await _db.Set<AppointmentDeposit>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == appointmentId, ct);
        if (d is null || d.Status is DepositStatus.Pending or DepositStatus.Paid) return null;
        var forfeited = d.Status == DepositStatus.Forfeited ? Math.Max(0, d.Amount - d.RefundAmount) : 0;
        return new CancellationOutcomeDto(appointmentId, d.FeeAmount > 0, d.FeeAmount, forfeited, d.RefundAmount,
            Math.Max(0, d.FeeAmount - forfeited), d.Status);
    }
}

// =====================================================================================
// Follow-up bookings
// =====================================================================================
public class FollowUpService : IFollowUpService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAppointmentService _appointments;
    public FollowUpService(ApplicationDbContext db, ICurrentUserService current, IAppointmentService appointments)
    { _db = db; _current = current; _appointments = appointments; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static FollowUpDto ToDto(AppointmentFollowUp f, string doctorName, string? petName) =>
        new(f.Id, f.SourceAppointmentId, f.DoctorId, doctorName, f.PetId, petName, f.InDays, f.DueDate, f.Note, f.Status, f.FollowUpAppointmentId);

    public async Task<FollowUpDto> RequestAsync(Guid sourceAppointmentId, RequestFollowUpInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var a = await _db.Appointments.Include(x => x.Doctor).ThenInclude(d => d.User).Include(x => x.Pet)
                    .SingleOrDefaultAsync(x => x.Id == sourceAppointmentId, ct) ?? throw new NotFoundException("Appointment", sourceAppointmentId);
        if (a.Doctor.UserId != uid) throw new ForbiddenException();
        if (a.Status is AppointmentStatus.CancelledByDoctor or AppointmentStatus.CancelledByUser or AppointmentStatus.Draft or AppointmentStatus.NoShow)
            throw new ConflictException("A follow-up can only be requested for an active or completed appointment.");
        if (input.InDays is < 1 or > 365)
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["inDays"] = new[] { "Choose between 1 and 365 days." } });

        var f = new AppointmentFollowUp
        {
            SourceAppointmentId = a.Id, DoctorId = a.DoctorId, PatientUserId = a.PatientUserId, PetId = a.PetId,
            InDays = input.InDays, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(input.InDays)),
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()[..Math.Min(1000, input.Note.Trim().Length)]
        };
        _db.Set<AppointmentFollowUp>().Add(f);
        _db.Notifications.Add(new InAppNotification
        {
            UserId = a.PatientUserId,
            Title = "Follow-up recommended",
            Body = $"{a.Doctor.User.DisplayName} recommends a follow-up visit in {input.InDays} day(s)" + (a.Pet is null ? "." : $" for {a.Pet.Name}."),
            Url = "/appointments?followUp=" + f.Id
        });
        await _db.SaveChangesAsync(ct);
        return ToDto(f, a.Doctor.User.DisplayName, a.Pet?.Name);
    }

    public async Task<IReadOnlyList<FollowUpDto>> MineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<AppointmentFollowUp>().AsNoTracking()
            .Where(f => f.PatientUserId == uid && f.Status == FollowUpStatus.Requested)
            .OrderBy(f => f.DueDate).Take(50).ToListAsync(ct);
        var dIds = rows.Select(r => r.DoctorId).Distinct().ToList();
        var pIds = rows.Where(r => r.PetId != null).Select(r => r.PetId!.Value).Distinct().ToList();
        var docs = await _db.Doctors.AsNoTracking().Where(d => dIds.Contains(d.Id)).Select(d => new { d.Id, d.User.DisplayName }).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        var pets = await _db.Pets.AsNoTracking().Where(p => pIds.Contains(p.Id)).Select(p => new { p.Id, p.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return rows.Select(r => ToDto(r, docs.GetValueOrDefault(r.DoctorId, "—"), r.PetId is { } pid ? pets.GetValueOrDefault(pid) : null)).ToList();
    }

    private async Task<Appointment> LoadSourceAsync(Guid id, CancellationToken ct)
    {
        var uid = Uid();
        var a = await _db.Appointments.AsNoTracking().Include(x => x.Doctor).ThenInclude(d => d.User).Include(x => x.Pet)
                    .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Appointment", id);
        if (a.PatientUserId != uid && a.Doctor.UserId != uid) throw new ForbiddenException();
        return a;
    }

    public async Task<FollowUpDto> PrefillAsync(Guid sourceAppointmentId, CancellationToken ct = default)
    {
        var a = await LoadSourceAsync(sourceAppointmentId, ct);
        var existing = await _db.Set<AppointmentFollowUp>().AsNoTracking()
            .Where(f => f.SourceAppointmentId == a.Id && f.Status == FollowUpStatus.Requested).OrderByDescending(f => f.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing is not null) return ToDto(existing, a.Doctor.User.DisplayName, a.Pet?.Name);
        var synth = new AppointmentFollowUp
        {
            Id = Guid.Empty, SourceAppointmentId = a.Id, DoctorId = a.DoctorId, PatientUserId = a.PatientUserId, PetId = a.PetId,
            InDays = 0, DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        return ToDto(synth, a.Doctor.User.DisplayName, a.Pet?.Name);
    }

    public async Task<AppointmentSummaryDto> BookAsync(Guid sourceAppointmentId, BookFollowUpInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var src = await LoadSourceAsync(sourceAppointmentId, ct);
        if (src.PatientUserId != uid) throw new ForbiddenException("Only the patient can book the follow-up.");
        if (src.Status is AppointmentStatus.CancelledByDoctor or AppointmentStatus.CancelledByUser or AppointmentStatus.Draft or AppointmentStatus.NoShow)
            throw new ConflictException("The original appointment was not held; book a regular appointment instead.");

        var dto = await _appointments.BookAsync(new BookAppointmentInput(src.DoctorId, input.TimeSlotId, src.PetId, src.Type, input.Symptoms), ct);

        // Link the new appointment to its origin (existing column) and close the open follow-up request.
        var created = await _db.Appointments.SingleAsync(x => x.Id == dto.Id, ct);
        created.FollowUpOfAppointmentId = src.Id;
        var req = await _db.Set<AppointmentFollowUp>()
            .Where(f => f.SourceAppointmentId == src.Id && f.Status == FollowUpStatus.Requested).OrderByDescending(f => f.CreatedAt).FirstOrDefaultAsync(ct);
        if (req is null)
        {
            req = new AppointmentFollowUp
            {
                SourceAppointmentId = src.Id, DoctorId = src.DoctorId, PatientUserId = src.PatientUserId, PetId = src.PetId,
                InDays = Math.Max(0, (int)Math.Round((created.ScheduledAt - src.ScheduledAt).TotalDays)),
                DueDate = DateOnly.FromDateTime(created.ScheduledAt)
            };
            _db.Set<AppointmentFollowUp>().Add(req);
        }
        req.Status = FollowUpStatus.Booked;
        req.FollowUpAppointmentId = created.Id;
        await _db.SaveChangesAsync(ct);
        return dto;
    }

    public async Task DismissAsync(Guid followUpId, CancellationToken ct = default)
    {
        var uid = Uid();
        var f = await _db.Set<AppointmentFollowUp>().SingleOrDefaultAsync(x => x.Id == followUpId, ct)
                ?? throw new NotFoundException("FollowUp", followUpId);
        if (f.PatientUserId != uid) throw new ForbiddenException();
        if (f.Status == FollowUpStatus.Requested) { f.Status = FollowUpStatus.Dismissed; await _db.SaveChangesAsync(ct); }
    }
}

// =====================================================================================
// Doctor switching
// =====================================================================================
public class DoctorSwitchService : IDoctorSwitchService
{
    private static readonly AppointmentStatus[] Switchable =
    {
        AppointmentStatus.PendingPayment, AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed, AppointmentStatus.Rescheduled
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ISlotLockService _slotLock;
    private readonly IUnitOfWork _uow;
    private readonly IKafkaProducer _kafka;
    private readonly IBookingPolicyService _policy;

    public DoctorSwitchService(ApplicationDbContext db, ICurrentUserService current, ISlotLockService slotLock,
        IUnitOfWork uow, IKafkaProducer kafka, IBookingPolicyService policy)
    { _db = db; _current = current; _slotLock = slotLock; _uow = uow; _kafka = kafka; _policy = policy; }

    private async Task<Appointment> LoadMineAsync(Guid id, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var a = await _db.Appointments.Include(x => x.Doctor).Include(x => x.Pet).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Appointment", id);
        if (a.PatientUserId != uid) throw new ForbiddenException();
        if (!Switchable.Contains(a.Status) || a.ScheduledAt <= DateTime.UtcNow)
            throw new ConflictException("Only upcoming appointments can be switched to another doctor.");
        return a;
    }

    private IQueryable<Doctor> EligibleDoctors(Appointment a)
    {
        var q = _db.Doctors.Where(d => d.Id != a.DoctorId && d.ApprovalStatus == ApprovalStatus.Approved);
        if (!string.IsNullOrWhiteSpace(a.Doctor.Specialty))
        {
            var spec = a.Doctor.Specialty!.ToLower();
            q = q.Where(d => d.Specialty != null && d.Specialty.ToLower() == spec);
        }
        if (a.Pet is not null) { var at = a.Pet.AnimalType; q = q.Where(d => d.SupportedAnimalTypes.Any(t => t.AnimalType == at)); }
        return q;
    }

    private static bool TypeFits(ConsultationType slotType, ConsultationType apptType) =>
        slotType == ConsultationType.Both || slotType == apptType;

    public async Task<IReadOnlyList<SwitchCandidateDto>> CandidatesAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var a = await LoadMineAsync(appointmentId, ct);
        var now = DateTime.UtcNow;
        var docs = await EligibleDoctors(a).AsNoTracking()
            .Select(d => new { d.Id, Name = d.User.DisplayName, d.Specialty, d.ConsultationFee, d.RatingAverage })
            .OrderByDescending(d => d.RatingAverage).Take(15).ToListAsync(ct);
        var ids = docs.Select(d => d.Id).ToList();
        var slots = await _db.DoctorTimeSlots.AsNoTracking()
            .Where(s => ids.Contains(s.DoctorId) && s.Status == SlotStatus.Available && s.StartUtc > now)
            .OrderBy(s => s.StartUtc).Take(400).ToListAsync(ct);
        var result = new List<SwitchCandidateDto>();
        foreach (var d in docs)
            foreach (var s in slots.Where(s => s.DoctorId == d.Id && TypeFits(s.ConsultationType, a.Type)).Take(3))
                result.Add(new SwitchCandidateDto(d.Id, d.Name, d.Specialty, d.ConsultationFee, d.RatingAverage, s.Id, s.StartUtc, s.EndUtc));
        return result;
    }

    public async Task<AppointmentSummaryDto> SwitchAsync(Guid appointmentId, SwitchDoctorInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var old = await LoadMineAsync(appointmentId, ct);
        if (input.NewDoctorId == old.DoctorId) throw new ConflictException("Pick a different doctor.");

        await using var releaser = await _slotLock.AcquireAsync(input.NewDoctorId, input.NewTimeSlotId, TimeSpan.FromSeconds(5), ct);

        AppointmentSummaryDto? result = null;
        DoctorTimeSlot? freedOld = null;
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            var nd = await EligibleDoctors(old).Include(d => d.User).SingleOrDefaultAsync(d => d.Id == input.NewDoctorId, token)
                     ?? throw new ConflictException("That doctor is not an eligible match (specialty, animal type or approval).");
            var slot = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == input.NewTimeSlotId && s.DoctorId == nd.Id, token)
                       ?? throw new NotFoundException("Slot", input.NewTimeSlotId);
            if (slot.Status != SlotStatus.Available || slot.StartUtc <= DateTime.UtcNow) throw new ConflictException("Slot is no longer available.");
            if (!TypeFits(slot.ConsultationType, old.Type)) throw new ConflictException("That slot does not support this consultation type.");

            var carriedPaid = old.PaymentStatus == PaymentStatus.Paid && old.Amount > 0 && nd.ConsultationFee <= old.Amount;
            var needsPayment = nd.ConsultationFee > 0 && !carriedPaid;
            var status = needsPayment ? AppointmentStatus.PendingPayment
                : (nd.AutoConfirmAppointments ? AppointmentStatus.Confirmed : AppointmentStatus.PendingConfirmation);
            var created = new Appointment
            {
                DoctorId = nd.Id, PatientUserId = uid, PetId = old.PetId, ScheduledAt = slot.StartUtc,
                DurationMinutes = (int)(slot.EndUtc - slot.StartUtc).TotalMinutes, Type = old.Type, Symptoms = old.Symptoms,
                Amount = nd.ConsultationFee, PaymentStatus = needsPayment ? PaymentStatus.Unpaid : PaymentStatus.Paid, Status = status,
                TimeSlotId = slot.Id, ConfirmedAt = status == AppointmentStatus.Confirmed ? DateTime.UtcNow : null
            };
            _db.Appointments.Add(created);
            slot.Status = SlotStatus.Booked;
            slot.AppointmentId = created.Id;
            await _policy.ApplyOnBookingAsync(created, token);
            if (!needsPayment) await _policy.OnPaidAsync(created, token);

            // Cancel the old appointment without penalty.
            if (old.TimeSlotId is { } oldSlotId)
            {
                var os = await _db.DoctorTimeSlots.SingleOrDefaultAsync(s => s.Id == oldSlotId, token);
                if (os is not null) { os.Status = SlotStatus.Available; os.AppointmentId = null; freedOld = os; }
            }
            old.Status = AppointmentStatus.CancelledByUser;
            old.CancellationReason = "Switched to another doctor";
            await _policy.ApplyCancellationAsync(old, byDoctor: false, waivePenalty: true, token);
            if (old.PaymentStatus == PaymentStatus.Paid && !carriedPaid && old.Amount > 0) old.PaymentStatus = PaymentStatus.Refunded;

            _db.Notifications.Add(new InAppNotification { UserId = old.Doctor.UserId, Title = "Patient switched to another doctor",
                Body = $"The appointment at {old.ScheduledAt:u} was moved to a different doctor.", Url = "/appointments" });
            _db.Notifications.Add(new InAppNotification { UserId = nd.UserId, Title = "New appointment (switched from another doctor)",
                Body = $"Scheduled at {created.ScheduledAt:u}", Url = $"/dashboard/vet?appointment={created.Id}" });
            _db.Notifications.Add(new InAppNotification { UserId = uid, Title = "Doctor switched",
                Body = $"Your appointment is now with {nd.User.DisplayName} at {created.ScheduledAt:u}.", Url = "/appointments" });

            await _kafka.PublishAsync(VetTopics.Appointments,
                new AppointmentBooked(created.Id, nd.Id, uid, created.ScheduledAt, created.Status.ToString(), DateTime.UtcNow), created.Id.ToString(), token);
            await _kafka.PublishAsync(VetTopics.Appointments,
                new AppointmentCancelled(old.Id, uid, false, old.CancellationReason, DateTime.UtcNow), old.Id.ToString(), token);

            result = new AppointmentSummaryDto(created.Id, nd.Id, nd.User.DisplayName, uid, "—", created.PetId, old.Pet?.Name,
                created.ScheduledAt, created.DurationMinutes, created.Type, created.Status, created.PaymentStatus, created.Amount,
                null, null, DateTime.UtcNow);
        }, ct);
        return result!;
    }
}

// =====================================================================================
// Shared calendars
// =====================================================================================
public class CalendarShareService : ICalendarShareService
{
    private const int MaxActiveShares = 10;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public CalendarShareService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static CalendarShareDto ToDto(CalendarShare s, string? token = null) =>
        new(s.Id, s.Scope, s.Label, s.TokenHint, s.CreatedAt, s.ExpiresAt, s.RevokedAt, s.LastAccessedAt,
            token is null ? null : $"/api/v1/calendar/shared/{token}.ics");

    public async Task<CalendarShareDto> CreateAsync(CreateCalendarShareInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (input.Scope == CalendarShareScope.Doctor && !await _db.Doctors.AnyAsync(d => d.UserId == uid, ct))
            throw new ForbiddenException("Only veterinarians can share a doctor calendar.");
        var now = DateTime.UtcNow;
        var active = await _db.Set<CalendarShare>().CountAsync(s => s.OwnerUserId == uid && s.RevokedAt == null && (s.ExpiresAt == null || s.ExpiresAt > now), ct);
        if (active >= MaxActiveShares) throw new ConflictException($"You can have at most {MaxActiveShares} active calendar links. Revoke one first.");
        var days = Math.Clamp(input.ExpiresInDays ?? 90, 1, 365);
        var token = NewToken();
        var label = input.Label?.Trim();
        var s = new CalendarShare
        {
            OwnerUserId = uid, TokenHash = HashToken(token), TokenHint = token[..6], Scope = input.Scope,
            Label = string.IsNullOrEmpty(label) ? null : label[..Math.Min(120, label.Length)], ExpiresAt = now.AddDays(days)
        };
        _db.Set<CalendarShare>().Add(s);
        await _db.SaveChangesAsync(ct);
        return ToDto(s, token);
    }

    public async Task<IReadOnlyList<CalendarShareDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.Set<CalendarShare>().AsNoTracking().Where(s => s.OwnerUserId == uid)
            .OrderByDescending(s => s.CreatedAt).Take(50).ToListAsync(ct);
        return rows.Select(s => ToDto(s)).ToList();
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var s = await _db.Set<CalendarShare>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid, ct)
                ?? throw new NotFoundException("CalendarShare", id);
        if (s.RevokedAt is null) { s.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
    }

    public async Task<string?> FeedAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 20 or > 80) return null;
        var hash = HashToken(token);
        var now = DateTime.UtcNow;
        var s = await _db.Set<CalendarShare>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (s is null || s.RevokedAt is not null || (s.ExpiresAt is { } e && e <= now)) return null;

        // Calendar clients poll often: touch LastAccessedAt at most every 10 minutes.
        if (s.LastAccessedAt is null || now - s.LastAccessedAt > TimeSpan.FromMinutes(10))
        {
            s.LastAccessedAt = now;
            await _db.SaveChangesAsync(ct);
        }

        var q = _db.Appointments.AsNoTracking().Where(a => a.ScheduledAt >= now.AddDays(-14) && a.ScheduledAt <= now.AddDays(365)
            && a.Status != AppointmentStatus.Draft && a.Status != AppointmentStatus.PendingPayment && a.Status != AppointmentStatus.Refunded);
        q = s.Scope == CalendarShareScope.Doctor ? q.Where(a => a.Doctor.UserId == s.OwnerUserId) : q.Where(a => a.PatientUserId == s.OwnerUserId);
        var rows = await q.OrderBy(a => a.ScheduledAt).Take(500).Select(a => new
        {
            a.Id, a.ScheduledAt, a.DurationMinutes, a.Type, a.Status, a.UpdatedAt, a.CreatedAt,
            Doctor = a.Doctor.User.DisplayName, Patient = a.PatientUser.DisplayName,
            Pet = a.Pet != null ? a.Pet.Name : null, a.Doctor.ClinicName, a.Doctor.ClinicAddress
        }).ToListAsync(ct);

        // Read-only feed for third parties: no symptoms, notes or join links.
        var events = rows.Select(r =>
        {
            var online = r.Type == ConsultationType.Online;
            var summary = s.Scope == CalendarShareScope.Doctor
                ? (online ? "Online appointment" : "Appointment") + (r.Pet is null ? "" : $" - {r.Pet}")
                : (online ? $"Online vet consultation with {r.Doctor}" : $"Vet visit with {r.Doctor}") + (r.Pet is null ? "" : $" ({r.Pet})");
            string? loc = online ? "Online" : string.Join(", ", new[] { r.ClinicName, r.ClinicAddress }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrEmpty(loc)) loc = null;
            var status = r.Status switch
            {
                AppointmentStatus.Confirmed or AppointmentStatus.Completed => "CONFIRMED",
                AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor => "CANCELLED",
                _ => "TENTATIVE"
            };
            return new IcsEvent($"appt-{r.Id:D}@pawzaroo.app", r.ScheduledAt, r.ScheduledAt.AddMinutes(r.DurationMinutes), summary, null, loc, status,
                r.UpdatedAt ?? r.CreatedAt);
        });
        return IcsWriter.Calendar(events);
    }
}
