using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Veterinary;

public enum SlotStatus { Available = 0, Held = 1, Booked = 2, Cancelled = 3, Blocked = 4 }

/// <summary>
/// Concrete bookable slot pre-generated from DoctorAvailability rules. Booking
/// flips Status -> Booked and AppointmentId is set. Hot index: (DoctorId,StartUtc).
/// </summary>
public class DoctorTimeSlot : AuditableEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ConsultationType ConsultationType { get; set; }
    public SlotStatus Status { get; set; } = SlotStatus.Available;
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
}

public class Prescription : AuditableEntity
{
    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = default!;
    public Guid IssuedById { get; set; }
    public Doctor IssuedBy { get; set; } = default!;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? FileUrl { get; set; }
    /// <summary>JSONB array: [{ drug, dose, frequency, duration, instructions }, ...]</summary>
    public string? ItemsJson { get; set; }
    public DateTime? ValidUntil { get; set; }
}
