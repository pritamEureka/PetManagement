using Pawzaroo.Application.Modules.Vet.Dtos;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Application.Modules.Vet.Services;

public enum AppointmentScope { Mine, ForMyClinic, Admin }

public interface IVetDoctorService
{
    Task<CursorPage<DoctorSummaryDto>> SearchAsync(DoctorSearchInput input, CancellationToken ct = default);
    Task<DoctorDetailDto?> GetAsync(Guid doctorId, CancellationToken ct = default);
    Task<DoctorDetailDto?> GetMyProfileAsync(CancellationToken ct = default);

    Task<Guid> RegisterAsync(RegisterDoctorInput input, CancellationToken ct = default);
    Task UpdateAsync(UpdateDoctorInput input, CancellationToken ct = default);

    // Credentials
    Task<Guid> AddCredentialAsync(AddCredentialInput input, CancellationToken ct = default);
    Task<IReadOnlyList<CredentialDocumentDto>> ListCredentialsAsync(Guid? doctorId, CancellationToken ct = default);
    Task VerifyCredentialAsync(Guid credentialId, bool verified, CancellationToken ct = default);

    // Admin
    Task ApproveAsync(Guid doctorId, string? adminNotes, CancellationToken ct = default);
    Task RejectAsync(Guid doctorId, string? adminNotes, CancellationToken ct = default);
    Task SuspendAsync(Guid doctorId, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<CommissionReportRow>> CommissionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<SpecialtyDto>> ListSpecialtiesAsync(CancellationToken ct = default);
}

public interface IDoctorAvailabilityService
{
    Task<IReadOnlyList<AvailabilityRuleDto>> ListRulesAsync(Guid doctorId, CancellationToken ct = default);
    Task<Guid> AddRuleAsync(AvailabilityRuleInput input, CancellationToken ct = default);
    Task RemoveRuleAsync(Guid ruleId, CancellationToken ct = default);

    Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(Guid doctorId, CancellationToken ct = default);
    Task<Guid> AddHolidayAsync(HolidayInput input, CancellationToken ct = default);
    Task RemoveHolidayAsync(Guid holidayId, CancellationToken ct = default);

    /// <summary>Generates concrete bookable slots from rules between [from, to]. Idempotent — duplicates are skipped.</summary>
    Task<int> GenerateSlotsAsync(GenerateSlotsInput input, CancellationToken ct = default);

    Task<IReadOnlyList<TimeSlotDto>> ListSlotsAsync(Guid doctorId, DateOnly from, DateOnly to,
                                                    ConsultationType? type = null, bool availableOnly = true,
                                                    CancellationToken ct = default);

    Task BlockSlotAsync(Guid slotId, CancellationToken ct = default);
    Task UnblockSlotAsync(Guid slotId, CancellationToken ct = default);
}

public interface IAppointmentService
{
    Task<CursorPage<AppointmentSummaryDto>> ListAsync(AppointmentScope scope, string? cursor, int pageSize,
                                                      AppointmentStatus? status = null, CancellationToken ct = default);
    Task<AppointmentSummaryDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Atomic booking — acquires a slot lock, validates availability, creates appointment in correct status.</summary>
    Task<AppointmentSummaryDto> BookAsync(BookAppointmentInput input, CancellationToken ct = default);

    Task RescheduleAsync(Guid appointmentId, RescheduleInput input, CancellationToken ct = default);
    Task CancelByUserAsync(Guid appointmentId, CancelInput input, CancellationToken ct = default);
    Task CancelByDoctorAsync(Guid appointmentId, CancelInput input, CancellationToken ct = default);
    Task ConfirmAsync(Guid appointmentId, CancellationToken ct = default);
    Task CompleteAsync(Guid appointmentId, CancellationToken ct = default);
    Task MarkNoShowAsync(Guid appointmentId, CancellationToken ct = default);
    Task MarkPaidAsync(Guid appointmentId, CancellationToken ct = default);  // payment placeholder
    Task RefundAsync(Guid appointmentId, CancellationToken ct = default);

    Task<string> EnsureMeetingLinkAsync(Guid appointmentId, CancellationToken ct = default);
}

public interface IPrescriptionService
{
    Task<Guid> UploadAsync(Guid appointmentId, PrescriptionInput input, CancellationToken ct = default);
    Task AddFollowUpAsync(Guid appointmentId, FollowUpInput input, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListForPatientAsync(CancellationToken ct = default);
}

public interface IDoctorReviewService
{
    Task<Guid> SubmitAsync(Guid appointmentId, ReviewInput input, CancellationToken ct = default);
    Task<IReadOnlyList<object>> ListForDoctorAsync(Guid doctorId, CancellationToken ct = default);
}

/// <summary>Distributed lock for atomic slot booking. Redis-backed.</summary>
public interface ISlotLockService
{
    Task<IAsyncDisposable> AcquireAsync(Guid doctorId, Guid slotId, TimeSpan timeout, CancellationToken ct = default);
}
