using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Communities;

public enum CommunityRole { Member = 0, Moderator = 1, Owner = 2 }

public enum CommunityMemberStatus { Active = 0, Pending = 1, Banned = 2 }

public enum CommunityEventType { Meetup = 0, VaccinationCamp = 1, AdoptionDrive = 2, Other = 3 }

public enum RsvpStatus { Going = 0, Waitlisted = 1, Cancelled = 2 }

/// <summary>Breed/species community. Private groups require owner/moderator approval to join.</summary>
public class Community : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Species { get; set; }
    public string? Breed { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? Description { get; set; }
    public string? City { get; set; }
    public Guid OwnerId { get; set; }
}

public class CommunityMember : AuditableEntity
{
    public Guid CommunityId { get; set; }
    public Guid UserId { get; set; }
    public CommunityRole Role { get; set; } = CommunityRole.Member;
    public CommunityMemberStatus Status { get; set; } = CommunityMemberStatus.Active;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class CommunityEvent : AuditableEntity
{
    public Guid CommunityId { get; set; }
    public Guid OrganizerId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public CommunityEventType Type { get; set; } = CommunityEventType.Meetup;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? City { get; set; }
    public string? Venue { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Null = unlimited. Going beyond this count become Waitlisted.</summary>
    public int? Capacity { get; set; }
    public bool IsCancelled { get; set; }
}

public class EventRsvp : AuditableEntity
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public RsvpStatus Status { get; set; } = RsvpStatus.Going;
    /// <summary>Queue order for the waitlist (reset whenever the user re-RSVPs).</summary>
    public DateTime RsvpAt { get; set; } = DateTime.UtcNow;
}
