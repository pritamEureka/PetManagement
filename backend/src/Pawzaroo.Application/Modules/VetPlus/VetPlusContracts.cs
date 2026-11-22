using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Application.Modules.VetPlus;

// ---------- Telemedicine ----------

public record TelemedicineRoomDto(
    Guid AppointmentId,
    string State,                 // NotOpen | Open | Closed
    string? JoinUrl,              // withheld from patients until consent + admitted
    DateTime OpensAt,
    DateTime ExpiresAt,
    bool ConsentGiven,
    string? PreConsultNote,
    DateTime? CheckedInAt,
    DateTime? AdmittedAt,
    bool IsDoctor,
    string PatientName,
    string DoctorName);

public record TelemedicineCheckInInput(bool Consent, string? PreConsultNote);

public record TelemedicineEventInput(TelemedicineEventKind Kind);

public record WaitingPatientDto(
    Guid AppointmentId, string PatientName, string? PetName,
    DateTime ScheduledAt, DateTime CheckedInAt, string? PreConsultNote);

public record ConsultationNoteDto(
    Guid AppointmentId, string? Subjective, string? Objective, string? Assessment, string? Plan, DateTime? UpdatedAt);

public record SaveConsultationNoteInput(string? Subjective, string? Objective, string? Assessment, string? Plan);

public interface ITelemedicineService
{
    Task<TelemedicineRoomDto> GetRoomAsync(Guid appointmentId, CancellationToken ct = default);
    Task<TelemedicineRoomDto> CheckInAsync(Guid appointmentId, TelemedicineCheckInInput input, CancellationToken ct = default);
    Task AdmitAsync(Guid appointmentId, CancellationToken ct = default);
    Task RecordEventAsync(Guid appointmentId, TelemedicineEventKind kind, CancellationToken ct = default);
    Task<IReadOnlyList<WaitingPatientDto>> WaitingListAsync(CancellationToken ct = default);
    Task<ConsultationNoteDto> GetNoteAsync(Guid appointmentId, CancellationToken ct = default);
    Task<ConsultationNoteDto> SaveNoteAsync(Guid appointmentId, SaveConsultationNoteInput input, CancellationToken ct = default);
}

// ---------- Waitlist ----------

public record JoinWaitlistInput(
    Guid DoctorId, Guid? PetId, DateOnly? PreferredFrom, DateOnly? PreferredTo,
    ConsultationType? ConsultationType, string? Note);

public record WaitlistEntryDto(
    Guid Id, Guid DoctorId, string DoctorName, WaitlistStatus Status, int Position,
    DateOnly? PreferredFrom, DateOnly? PreferredTo, ConsultationType? ConsultationType,
    string? Note, DateTime CreatedAt, DateTime? NotifiedAt);

public interface IWaitlistService
{
    Task<WaitlistEntryDto> JoinAsync(JoinWaitlistInput input, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(CancellationToken ct = default);
    Task LeaveAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Hook invoked by the appointment cancel path when a slot becomes free.</summary>
public interface IWaitlistNotifier
{
    Task OnSlotFreedAsync(Guid doctorId, DateTime slotStartUtc, DateTime slotEndUtc,
        ConsultationType slotType, Guid? excludeUserId, CancellationToken ct = default);
}

// ---------- Recurring ----------

public record BookSeriesInput(
    Guid DoctorId, Guid TimeSlotId, Guid? PetId, ConsultationType Type, string? Symptoms,
    RecurrenceFrequency Frequency, int Occurrences);

public record SeriesBookedItem(int Sequence, Guid AppointmentId, DateTime ScheduledAt);
public record SeriesFailedItem(int Sequence, DateTime DesiredStartUtc, string Reason);
public record BookSeriesResult(Guid? SeriesId, IReadOnlyList<SeriesBookedItem> Booked, IReadOnlyList<SeriesFailedItem> Failed);

public interface IRecurringBookingService
{
    Task<BookSeriesResult> BookSeriesAsync(BookSeriesInput input, CancellationToken ct = default);
}

// ---------- Calendar ----------

public interface ICalendarExportService
{
    Task<string> AppointmentIcsAsync(Guid appointmentId, CancellationToken ct = default);
    Task<string> UpcomingIcsAsync(CancellationToken ct = default);
}
