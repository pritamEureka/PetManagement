using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Domain.Social;

namespace Pawzaroo.Application.Modules.Feed;

public record NearbyFeedQuery(double? Lat, double? Lng, double? RadiusKm, string? City, string? Cursor, int PageSize = 20);

public record NearbyFeedItemDto(FeedItemDto Post, double? DistanceKm, string? City);

public record PetAlbumPhotoDto(Guid Id, string Url, string? Caption, DateTime? TakenAt, int SortOrder);

public record PetAlbumDto(Guid Id, Guid PetId, string Title, AlbumVisibility Visibility, string? CoverUrl, int PhotoCount,
    IReadOnlyList<PetAlbumPhotoDto> Photos);

public record CreatePetAlbumRequest(string Title, AlbumVisibility Visibility, string? CoverUrl);
public record UpdatePetAlbumRequest(string? Title, AlbumVisibility? Visibility, string? CoverUrl);
public record AddAlbumPhotoRequest(string Url, string? Caption, DateTime? TakenAt);

public record PetSocialProfileDto(Guid PetId, string Name, string? PhotoUrl, string AnimalType, string? Breed,
    int FollowerCount, bool IsFollowing, bool IsOwner, IReadOnlyList<PetAlbumDto> Albums);

public record FollowedPetPhotoDto(Guid PetId, string PetName, Guid AlbumId, string AlbumTitle, PetAlbumPhotoDto Photo);

public interface INearbyFeedService
{
    Task<CursorPage<NearbyFeedItemDto>> GetNearbyAsync(NearbyFeedQuery q, CancellationToken ct = default);
}

public interface IPetSocialService
{
    /// <summary>Profile by pet id. Visible when the owner enabled the public profile (or caller owns the pet).
    /// Albums are filtered server-side by visibility: Public for all, Followers for followers, Private for the owner.</summary>
    Task<PetSocialProfileDto> GetProfileAsync(Guid petId, CancellationToken ct = default);
    /// <summary>Same as <see cref="GetProfileAsync"/> keyed by the QR public code (anonymous callers allowed).</summary>
    Task<PetSocialProfileDto?> GetProfileByCodeAsync(string code, CancellationToken ct = default);
    Task FollowAsync(Guid petId, CancellationToken ct = default);
    Task UnfollowAsync(Guid petId, CancellationToken ct = default);
    Task<IReadOnlyList<FollowedPetPhotoDto>> FollowedPhotosAsync(int take, CancellationToken ct = default);

    Task<PetAlbumDto> CreateAlbumAsync(Guid petId, CreatePetAlbumRequest req, CancellationToken ct = default);
    Task<PetAlbumDto> UpdateAlbumAsync(Guid albumId, UpdatePetAlbumRequest req, CancellationToken ct = default);
    Task DeleteAlbumAsync(Guid albumId, CancellationToken ct = default);
    Task<PetAlbumPhotoDto> AddPhotoAsync(Guid albumId, AddAlbumPhotoRequest req, CancellationToken ct = default);
    Task DeletePhotoAsync(Guid albumId, Guid photoId, CancellationToken ct = default);
}
