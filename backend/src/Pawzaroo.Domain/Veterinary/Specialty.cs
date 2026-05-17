using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Veterinary;

public class Specialty : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DoctorSpecialty> Doctors { get; set; } = new List<DoctorSpecialty>();
}

public class DoctorSpecialty
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = default!;
    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = default!;
}
