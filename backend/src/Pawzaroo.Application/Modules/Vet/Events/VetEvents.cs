namespace Pawzaroo.Application.Modules.Vet.Events;

public static class VetTopics
{
    public const string Doctors      = "pawzaroo.vet.doctor.events";
    public const string Appointments = "pawzaroo.vet.appointment.events";
}

public record DoctorRegistered(Guid DoctorId, Guid UserId, DateTime At);
public record DoctorApproved(Guid DoctorId, Guid ApprovedBy, DateTime At);
public record DoctorRejected(Guid DoctorId, Guid RejectedBy, string? Reason, DateTime At);
public record DoctorSuspended(Guid DoctorId, Guid SuspendedBy, string? Reason, DateTime At);
public record CredentialVerified(Guid CredentialId, Guid DoctorId, Guid VerifierId, bool Verified, DateTime At);

public record AppointmentBooked(Guid AppointmentId, Guid DoctorId, Guid PatientUserId,
                                DateTime ScheduledAt, string Status, DateTime At);
public record AppointmentConfirmed(Guid AppointmentId, DateTime At);
public record AppointmentRescheduled(Guid AppointmentId, DateTime NewScheduledAt, DateTime At);
public record AppointmentCancelled(Guid AppointmentId, Guid CancelledBy, bool ByDoctor, string? Reason, DateTime At);
public record AppointmentCompleted(Guid AppointmentId, DateTime At);
public record AppointmentNoShow(Guid AppointmentId, DateTime At);
public record AppointmentPaid(Guid AppointmentId, decimal Amount, DateTime At);
public record AppointmentRefunded(Guid AppointmentId, decimal Amount, DateTime At);
public record PrescriptionUploaded(Guid AppointmentId, Guid DoctorId, DateTime At);
public record DoctorReviewed(Guid DoctorId, Guid ReviewerId, int Rating, DateTime At);
