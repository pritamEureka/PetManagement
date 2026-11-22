using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Veterinary;

/// <summary>Time-bound telemedicine room attached 1:1 to an Online appointment.</summary>
public class TelemedicineRoom : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public string RoomName { get; set; } = default!;
    public string JoinUrl { get; set; } = default!;
    public DateTime OpensAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentAt { get; set; }
    public string? PreConsultNote { get; set; }
    public DateTime? PatientCheckedInAt { get; set; }
    public DateTime? PatientAdmittedAt { get; set; }
}

public enum TelemedicineEventKind { CheckIn = 0, Admit = 1, Join = 2, Leave = 3, Closed = 4 }

public class TelemedicineEvent : AuditableEntity
{
    public Guid RoomId { get; set; }
    public TelemedicineRoom Room { get; set; } = default!;
    public Guid? UserId { get; set; }
    public TelemedicineEventKind Kind { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

/// <summary>Post-call SOAP note, one per appointment.</summary>
public class ConsultationNote : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public Guid AuthorDoctorId { get; set; }
    public Doctor AuthorDoctor { get; set; } = default!;
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
}

public enum WaitlistStatus { Waiting = 0, Notified = 1, Fulfilled = 2, Cancelled = 3 }

public class AppointmentWaitlistEntry : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public Guid UserId { get; set; }
    public Guid? PetId { get; set; }
    public DateOnly? PreferredFrom { get; set; }
    public DateOnly? PreferredTo { get; set; }
    public ConsultationType? ConsultationType { get; set; }
    public string? Note { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
    public DateTime? NotifiedAt { get; set; }
}

public enum RecurrenceFrequency { Weekly = 0, Biweekly = 1, Monthly = 2 }

public class AppointmentSeries : AuditableEntity
{
    public Guid PatientUserId { get; set; }
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public RecurrenceFrequency Frequency { get; set; }
    public int Occurrences { get; set; }
    public ICollection<AppointmentSeriesItem> Items { get; set; } = new List<AppointmentSeriesItem>();
}

public class AppointmentSeriesItem : AuditableEntity
{
    public Guid SeriesId { get; set; }
    public AppointmentSeries Series { get; set; } = default!;
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public int Sequence { get; set; }
}
