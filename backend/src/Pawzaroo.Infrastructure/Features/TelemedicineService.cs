using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.VetPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

public class TelemedicineService : ITelemedicineService
{
    private static readonly TimeSpan EarlyJoin = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan GraceAfterEnd = TimeSpan.FromMinutes(30);
    private const int MaxNoteLength = 4000;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IConfiguration _config;
    private readonly Application.Modules.Messaging.Services.IMessagingService _messaging;
    private readonly ILogger<TelemedicineService> _log;
    private readonly Application.Modules.ClinicPlus.IQuestionnaireService? _questionnaires;

    public TelemedicineService(ApplicationDbContext db, ICurrentUserService current, IConfiguration config,
        Application.Modules.Messaging.Services.IMessagingService messaging, ILogger<TelemedicineService> log,
        Application.Modules.ClinicPlus.IQuestionnaireService? questionnaires = null)
    {
        _questionnaires = questionnaires;
        _messaging = messaging;
        _log = log;
        _db = db;
        _current = current;
        _config = config;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private sealed record Ctx(Appointment Appt, bool IsDoctor);

    private async Task<Ctx> LoadAsync(Guid appointmentId, CancellationToken ct)
    {
        var uid = Uid();
        var appt = await _db.Appointments
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.PatientUser)
            .SingleOrDefaultAsync(a => a.Id == appointmentId, ct)
            ?? throw new NotFoundException("Appointment", appointmentId);
        var isDoctor = appt.Doctor.UserId == uid;
        if (!isDoctor && appt.PatientUserId != uid) throw new ForbiddenException();
        return new Ctx(appt, isDoctor);
    }

    private static bool IsClosedStatus(AppointmentStatus s) =>
        s is AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor
            or AppointmentStatus.NoShow or AppointmentStatus.Refunded
            or AppointmentStatus.Draft or AppointmentStatus.PendingPayment;

    private async Task<TelemedicineRoom> EnsureRoomAsync(Appointment appt, CancellationToken ct)
    {
        var room = await _db.Set<TelemedicineRoom>().SingleOrDefaultAsync(r => r.AppointmentId == appt.Id, ct);
        if (room is not null) return room;

        if (appt.Type != ConsultationType.Online)
            throw new AppException("not_online", "This appointment is not an online consultation.");
        if (IsClosedStatus(appt.Status))
            throw new ConflictException("No room is available for an appointment in this status.");

        var baseUrl = (_config["Telemedicine:BaseUrl"] ?? "https://meet.jit.si").TrimEnd('/');
        var roomName = "pawzaroo-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        room = new TelemedicineRoom
        {
            AppointmentId = appt.Id,
            RoomName = roomName,
            JoinUrl = $"{baseUrl}/{roomName}",
            OpensAt = appt.ScheduledAt - EarlyJoin,
            ExpiresAt = appt.ScheduledAt.AddMinutes(appt.DurationMinutes) + GraceAfterEnd
        };
        _db.Set<TelemedicineRoom>().Add(room);
        await _db.SaveChangesAsync(ct);
        return room;
    }

    private static string StateOf(TelemedicineRoom r)
    {
        var now = DateTime.UtcNow;
        if (r.ClosedAt is not null || now > r.ExpiresAt) return "Closed";
        return now < r.OpensAt ? "NotOpen" : "Open";
    }

    private static TelemedicineRoomDto ToDto(Ctx c, TelemedicineRoom r)
    {
        var state = StateOf(r);
        string? url = null;
        if (state != "Closed")
        {
            if (c.IsDoctor) url = r.JoinUrl;
            else if (state == "Open" && r.ConsentGiven && r.PatientAdmittedAt is not null) url = r.JoinUrl;
        }
        return new TelemedicineRoomDto(c.Appt.Id, state, url, r.OpensAt, r.ExpiresAt, r.ConsentGiven,
            r.PreConsultNote, r.PatientCheckedInAt, r.PatientAdmittedAt, c.IsDoctor,
            c.Appt.PatientUser.DisplayName, c.Appt.Doctor.User.DisplayName);
    }

    public async Task<TelemedicineRoomDto> GetRoomAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        var room = await EnsureRoomAsync(c.Appt, ct);
        return ToDto(c, room);
    }

    public async Task<TelemedicineRoomDto> CheckInAsync(Guid appointmentId, TelemedicineCheckInInput input, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        if (c.IsDoctor) throw new ForbiddenException("Only the patient checks in.");
        if (!input.Consent) throw new AppException("consent_required", "Consent to the video consultation is required.");
        var note = input.PreConsultNote?.Trim();
        if (note is { Length: > 2000 }) throw new AppException("note_too_long", "Note is too long (max 2000 characters).");

        if (_questionnaires is not null) await _questionnaires.EnsureCompletedIfRequiredAsync(c.Appt.Id, ct);
        var room = await EnsureRoomAsync(c.Appt, ct);
        if (StateOf(room) != "Open") throw new ConflictException("The room is not open for check-in right now.");

        var firstCheckIn = room.PatientCheckedInAt is null;
        room.ConsentGiven = true;
        room.ConsentAt ??= DateTime.UtcNow;
        room.PreConsultNote = string.IsNullOrEmpty(note) ? room.PreConsultNote : note;
        if (firstCheckIn)
        {
            room.PatientCheckedInAt = DateTime.UtcNow;
            _db.Set<TelemedicineEvent>().Add(new TelemedicineEvent
            { RoomId = room.Id, UserId = c.Appt.PatientUserId, Kind = TelemedicineEventKind.CheckIn });
            _db.Notifications.Add(new InAppNotification
            {
                UserId = c.Appt.Doctor.UserId,
                Title = "Patient is in the waiting room",
                Body = $"{c.Appt.PatientUser.DisplayName} checked in for the online consultation.",
                Url = $"/appointments/{c.Appt.Id}/room"
            });
        }
        await _db.SaveChangesAsync(ct);

        // Also deliver the pre-consult note to the vet as a chat message. Best effort: a block or a
        // messaging failure must never fail the check-in itself.
        if (firstCheckIn && !string.IsNullOrEmpty(note))
        {
            try
            {
                await _messaging.StartConversationAsync(new Application.Modules.Messaging.Dtos.StartConversationInput(
                    c.Appt.Doctor.UserId, "appointment", c.Appt.Id, $"Pre-consult note: {note}"), ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not post pre-consult note to chat for appointment {AppointmentId}", c.Appt.Id);
            }
        }
        return ToDto(c, room);
    }

    public async Task AdmitAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        if (!c.IsDoctor) throw new ForbiddenException();
        var room = await EnsureRoomAsync(c.Appt, ct);
        if (StateOf(room) == "Closed") throw new ConflictException("The room is closed.");
        if (room.PatientCheckedInAt is null) throw new ConflictException("The patient has not checked in yet.");
        if (room.PatientAdmittedAt is not null) return;

        room.PatientAdmittedAt = DateTime.UtcNow;
        _db.Set<TelemedicineEvent>().Add(new TelemedicineEvent
        { RoomId = room.Id, UserId = Uid(), Kind = TelemedicineEventKind.Admit });
        _db.Notifications.Add(new InAppNotification
        {
            UserId = c.Appt.PatientUserId,
            Title = "Your vet is ready",
            Body = "You have been admitted to the consultation room. Tap to join.",
            Url = $"/appointments/{c.Appt.Id}/room"
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordEventAsync(Guid appointmentId, TelemedicineEventKind kind, CancellationToken ct = default)
    {
        if (kind is not (TelemedicineEventKind.Join or TelemedicineEventKind.Leave))
            throw new AppException("invalid_event", "Only Join and Leave events can be recorded.");
        var c = await LoadAsync(appointmentId, ct);
        var room = await EnsureRoomAsync(c.Appt, ct);
        if (StateOf(room) == "Closed") throw new ConflictException("The room is closed.");
        if (!c.IsDoctor && room.PatientAdmittedAt is null)
            throw new ForbiddenException("You have not been admitted yet.");
        _db.Set<TelemedicineEvent>().Add(new TelemedicineEvent { RoomId = room.Id, UserId = Uid(), Kind = kind });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WaitingPatientDto>> WaitingListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        return await _db.Set<TelemedicineRoom>().AsNoTracking()
            .Where(r => r.Appointment.Doctor.UserId == uid
                        && r.PatientCheckedInAt != null && r.PatientAdmittedAt == null
                        && r.ClosedAt == null && r.ExpiresAt > now)
            .OrderBy(r => r.PatientCheckedInAt)
            .Select(r => new WaitingPatientDto(
                r.AppointmentId, r.Appointment.PatientUser.DisplayName,
                r.Appointment.Pet != null ? r.Appointment.Pet.Name : null,
                r.Appointment.ScheduledAt, r.PatientCheckedInAt!.Value, r.PreConsultNote))
            .ToListAsync(ct);
    }

    public async Task<ConsultationNoteDto> GetNoteAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        var n = await _db.Set<ConsultationNote>().AsNoTracking().SingleOrDefaultAsync(x => x.AppointmentId == c.Appt.Id, ct);
        return n is null
            ? new ConsultationNoteDto(c.Appt.Id, null, null, null, null, null)
            : ToDto(n);
    }

    public async Task<ConsultationNoteDto> SaveNoteAsync(Guid appointmentId, SaveConsultationNoteInput input, CancellationToken ct = default)
    {
        var c = await LoadAsync(appointmentId, ct);
        if (!c.IsDoctor) throw new ForbiddenException();
        foreach (var s in new[] { input.Subjective, input.Objective, input.Assessment, input.Plan })
            if (s is { Length: > MaxNoteLength })
                throw new AppException("note_too_long", $"Each SOAP section is limited to {MaxNoteLength} characters.");

        var n = await _db.Set<ConsultationNote>().SingleOrDefaultAsync(x => x.AppointmentId == c.Appt.Id, ct);
        if (n is null)
        {
            n = new ConsultationNote { AppointmentId = c.Appt.Id, AuthorDoctorId = c.Appt.DoctorId };
            _db.Set<ConsultationNote>().Add(n);
        }
        n.Subjective = input.Subjective;
        n.Objective = input.Objective;
        n.Assessment = input.Assessment;
        n.Plan = input.Plan;
        n.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    private static ConsultationNoteDto ToDto(ConsultationNote n) =>
        new(n.AppointmentId, n.Subjective, n.Objective, n.Assessment, n.Plan, n.UpdatedAt ?? n.CreatedAt);
}
