using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Pets;

public enum LostFoundKind { Lost = 0, Found = 1 }

public enum LostFoundStatus { Open = 0, Reunited = 1, Closed = 2 }

/// <summary>
/// Public lost / found alert. A Lost report usually references one of the
/// reporter's pets; a Found report describes an unknown animal.
/// </summary>
public class LostFoundReport : AuditableEntity
{
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = default!;
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public LostFoundKind Kind { get; set; }
    public LostFoundStatus Status { get; set; } = LostFoundStatus.Open;
    public AnimalType AnimalType { get; set; }
    public string? Breed { get; set; }
    public string? Color { get; set; }
    public string Description { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public DateTime LastSeenAt { get; set; }
    public string City { get; set; } = default!;
    public string? Area { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public ContactPreference ContactPreference { get; set; } = ContactPreference.Chat;
    public string? ContactPhone { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? FeedPostId { get; set; }
}
