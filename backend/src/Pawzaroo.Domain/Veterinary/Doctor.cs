using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Domain.Veterinary;

public class Doctor : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string LicenseNumber { get; set; } = default!;
    public string? Specialty { get; set; }
    public int? ExperienceYears { get; set; }
    public string? About { get; set; }
    public string? ClinicName { get; set; }
    public string? ClinicAddress { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public decimal ConsultationFee { get; set; }
    public ConsultationType ConsultationType { get; set; }
    public bool OnlineAvailable { get; set; }
    public bool OfflineAvailable { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public string? AdminNotes { get; set; }
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }

    /// <summary>If true, booked appointments skip PendingConfirmation and go straight to Confirmed.</summary>
    public bool AutoConfirmAppointments { get; set; }
    /// <summary>Default duration in minutes for generated slots.</summary>
    public int DefaultSlotMinutes { get; set; } = 30;
    /// <summary>Hours before appointment when cancellation is no longer free.</summary>
    public int CancellationCutoffHours { get; set; } = 12;

    public ICollection<DoctorCredentialDocument> CredentialDocuments { get; set; } = new List<DoctorCredentialDocument>();
    public ICollection<DoctorAnimalType> SupportedAnimalTypes { get; set; } = new List<DoctorAnimalType>();
    public ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
    public ICollection<DoctorHoliday> Holidays { get; set; } = new List<DoctorHoliday>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<DoctorReview> Reviews { get; set; } = new List<DoctorReview>();
}

public class DoctorAnimalType
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public AnimalType AnimalType { get; set; }
}

public class DoctorAvailability : BaseEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotMinutes { get; set; } = 30;
    public ConsultationType ConsultationType { get; set; }
}

public class DoctorHoliday : BaseEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }
}

public class Appointment : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public Guid PatientUserId { get; set; }
    public User PatientUser { get; set; } = default!;
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public ConsultationType Type { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Draft;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public decimal Amount { get; set; }
    public string? Symptoms { get; set; }
    public string? CancellationReason { get; set; }
    public string? PrescriptionFileUrl { get; set; }
    public string? FollowUpNotes { get; set; }
    public string? MeetingLink { get; set; }                  // for online consultations
    public Guid? TimeSlotId { get; set; }
    public DoctorTimeSlot? TimeSlot { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? FollowUpOfAppointmentId { get; set; }
    public Appointment? FollowUpOf { get; set; }
}

public class DoctorReview : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
