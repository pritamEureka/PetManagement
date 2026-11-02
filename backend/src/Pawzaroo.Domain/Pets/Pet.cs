using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Domain.Pets;

public class Pet : AuditableEntity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;

    public string Name { get; set; } = default!;
    public AnimalType AnimalType { get; set; }
    public string? Breed { get; set; }
    public Gender Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Color { get; set; }
    public string? TagNumber { get; set; }
    public string? PrimaryPhotoUrl { get; set; }
    public string? Allergies { get; set; }
    public string? DietNotes { get; set; }
    public bool IsAvailableForAdoption { get; set; }
    public AnimalSize? Size { get; set; }

    // Identification / public QR profile
    public string? MicrochipNumber { get; set; }
    public string? PublicCode { get; set; }
    public bool PublicProfileEnabled { get; set; }
    public bool IsLost { get; set; }

    // Lineage (breeders) and herd/flock grouping (livestock)
    public Guid? SireId { get; set; }
    public Pet? Sire { get; set; }
    public Guid? DamId { get; set; }
    public Pet? Dam { get; set; }
    public Guid? GroupId { get; set; }
    public PetGroup? Group { get; set; }

    public ICollection<PetPhoto> Photos { get; set; } = new List<PetPhoto>();
    public ICollection<VaccinationRecord> Vaccinations { get; set; } = new List<VaccinationRecord>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<GroomingRecord> GroomingRecords { get; set; } = new List<GroomingRecord>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<PetWeightLog> WeightLogs { get; set; } = new List<PetWeightLog>();
    public ICollection<PetMedication> Medications { get; set; } = new List<PetMedication>();
    public ICollection<PetCaretaker> Caretakers { get; set; } = new List<PetCaretaker>();
}

public class PetPhoto : BaseEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class VaccinationRecord : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public string VaccineName { get; set; } = default!;
    public DateTime AdministeredOn { get; set; }
    public DateTime? NextDueOn { get; set; }
    public string? AdministeredByVet { get; set; }
    public string? Notes { get; set; }
    public string? CertificateUrl { get; set; }
    /// <summary>Days-before-due of the last reminder sent (7, 1, 0 = overdue). Null = none sent yet.</summary>
    public int? LastReminderStage { get; set; }
}

public class MedicalRecord : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public DateTime VisitDate { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public string? PrescriptionFileUrl { get; set; }
    public string? Notes { get; set; }
    public string? VetName { get; set; }
    public string? AttachmentUrl { get; set; }
}

public class GroomingRecord : AuditableEntity
{
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
    public DateTime PerformedOn { get; set; }
    public string ServiceType { get; set; } = default!;
    public string? Provider { get; set; }
    public string? Notes { get; set; }
    public decimal? Cost { get; set; }
    public Guid? ServiceBookingId { get; set; }
}
