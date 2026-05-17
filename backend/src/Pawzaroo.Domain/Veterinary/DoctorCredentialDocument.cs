using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Veterinary;

public enum CredentialKind { License = 1, BoardCertification = 2, Diploma = 3, Insurance = 4, Other = 99 }

public class DoctorCredentialDocument : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public CredentialKind Kind { get; set; }
    public string Title { get; set; } = default!;
    public string FileUrl { get; set; } = default!;
    public string? IssuingAuthority { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public bool Verified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public User? VerifiedByUser { get; set; }
}

public class AppointmentDispute : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public Guid OpenedByUserId { get; set; }
    public User OpenedByUser { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public string? Details { get; set; }
    public AppointmentDisputeStatus Status { get; set; } = AppointmentDisputeStatus.Open;
    public Guid? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}
