using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Application.Modules.Communities;

public record CommunityPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record CommunityQuery(string? Q, string? Species, string? City, bool Mine = false, int Page = 1, int PageSize = 20, CommunityKind? Kind = null);

public record CommunityDto(
    Guid Id, string Name, string Slug, string? Species, string? Breed, bool IsPublic,
    string? Description, string? City, Guid OwnerId, int MemberCount,
    CommunityRole? MyRole, CommunityMemberStatus? MyStatus, DateTime CreatedAt, CommunityKind Kind = CommunityKind.Community);

public record CreateCommunityRequest(
    string Name, string? Species, string? Breed, bool IsPublic, string? Description, string? City, CommunityKind Kind = CommunityKind.Community);

public record CommunityMemberDto(
    Guid UserId, string DisplayName, string? AvatarUrl, CommunityRole Role,
    CommunityMemberStatus Status, DateTime JoinedAt);

public enum MemberAction { Approve, Ban, Unban, Promote, Demote, Remove }

public record CommunityEventDto(
    Guid Id, Guid CommunityId, string CommunityName, Guid OrganizerId, string Title, string? Description,
    CommunityEventType Type, DateTime StartsAt, DateTime EndsAt, string? City, string? Venue,
    double? Latitude, double? Longitude, int? Capacity, bool IsCancelled,
    int GoingCount, int WaitlistCount, RsvpStatus? MyRsvp, double? DistanceKm);

public record CreateCommunityEventRequest(
    string Title, string? Description, CommunityEventType Type, DateTime StartsAt, DateTime EndsAt,
    string? City, string? Venue, double? Latitude, double? Longitude, int? Capacity);

public record EventDiscoveryQuery(
    double? Lat, double? Lng, double? RadiusKm, string? City, CommunityEventType? Type,
    DateTime? From, DateTime? To, int Page = 1, int PageSize = 20);

public record EventAttendeeDto(Guid UserId, string DisplayName, RsvpStatus Status, DateTime RsvpAt);

public interface ICommunityService
{
    Task<CommunityPage<CommunityDto>> ListAsync(CommunityQuery q, CancellationToken ct = default);
    Task<CommunityDto> GetAsync(string idOrSlug, CancellationToken ct = default);
    Task<CommunityDto> CreateAsync(CreateCommunityRequest req, CancellationToken ct = default);
    Task<CommunityDto> JoinAsync(Guid communityId, CancellationToken ct = default);
    Task LeaveAsync(Guid communityId, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityMemberDto>> ListMembersAsync(Guid communityId, CommunityMemberStatus? status, CancellationToken ct = default);
    Task ModerateAsync(Guid communityId, Guid targetUserId, MemberAction action, CancellationToken ct = default);

    Task<CommunityEventDto> CreateEventAsync(Guid communityId, CreateCommunityEventRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<CommunityEventDto>> ListCommunityEventsAsync(Guid communityId, bool includePast, CancellationToken ct = default);
    Task<CommunityPage<CommunityEventDto>> DiscoverEventsAsync(EventDiscoveryQuery q, CancellationToken ct = default);
    /// <summary>Upcoming events from communities the caller is an active member of.</summary>
    Task<CommunityPage<CommunityEventDto>> GroupEventFeedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<CommunityEventDto> GetEventAsync(Guid eventId, CancellationToken ct = default);
    Task<CommunityEventDto> RsvpAsync(Guid eventId, CancellationToken ct = default);
    Task<CommunityEventDto> CancelRsvpAsync(Guid eventId, CancellationToken ct = default);
    Task CancelEventAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<EventAttendeeDto>> ListAttendeesAsync(Guid eventId, CancellationToken ct = default);
}
