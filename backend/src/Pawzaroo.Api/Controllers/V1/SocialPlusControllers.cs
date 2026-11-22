using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Application.Modules.Feed;
using Pawzaroo.Application.Modules.Feed.Dtos;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Community polls and reputation. Membership/moderation is enforced in the service.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/community-plus")]
[Authorize]
public class CommunityPlusController : ControllerBase
{
    private readonly ICommunityPlusService _svc;
    public CommunityPlusController(ICommunityPlusService svc) => _svc = svc;

    [HttpGet("{communityId:guid}/polls")]
    public Task<IReadOnlyList<PollDto>> Polls(Guid communityId, CancellationToken ct) => _svc.ListPollsAsync(communityId, ct);

    [HttpPost("{communityId:guid}/polls")]
    [EnableRateLimiting("writes")]
    public Task<PollDto> CreatePoll(Guid communityId, [FromBody] CreatePollRequest req, CancellationToken ct) => _svc.CreatePollAsync(communityId, req, ct);

    [HttpPost("polls/{pollId:guid}/vote")]
    [EnableRateLimiting("writes")]
    public Task<PollDto> Vote(Guid pollId, [FromBody] PollVoteRequest req, CancellationToken ct) => _svc.VoteAsync(pollId, req, ct);

    [HttpPost("polls/{pollId:guid}/close")]
    public Task<PollDto> Close(Guid pollId, CancellationToken ct) => _svc.ClosePollAsync(pollId, ct);

    [HttpDelete("polls/{pollId:guid}")]
    public async Task<IActionResult> DeletePoll(Guid pollId, CancellationToken ct)
    {
        await _svc.DeletePollAsync(pollId, ct);
        return NoContent();
    }

    [HttpGet("{communityId:guid}/leaderboard")]
    public Task<IReadOnlyList<ReputationDto>> Leaderboard(Guid communityId, [FromQuery] int take = 10, CancellationToken ct = default)
        => _svc.LeaderboardAsync(communityId, take, ct);

    [HttpGet("reputation/me")]
    public Task<MyReputationDto> MyReputation([FromQuery] Guid? communityId, CancellationToken ct) => _svc.MyReputationAsync(communityId, ct);

    [HttpPost("reputation/badges")]
    public Task<IReadOnlyDictionary<Guid, ReputationBadgeDto>> Badges([FromBody] List<Guid> userIds, CancellationToken ct)
        => _svc.BadgesAsync(userIds, ct);
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/posts/nearby")]
[Authorize]
public class NearbyFeedController : ControllerBase
{
    private readonly INearbyFeedService _svc;
    public NearbyFeedController(INearbyFeedService svc) => _svc = svc;

    [HttpGet]
    public Task<CursorPage<NearbyFeedItemDto>> Nearby([FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radiusKm,
        [FromQuery] string? city, [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.GetNearbyAsync(new NearbyFeedQuery(lat, lng, radiusKm, city, cursor, pageSize), ct);
}

/// <summary>Followable pet profiles and owner-managed albums.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/pet-social")]
[Authorize]
public class PetSocialController : ControllerBase
{
    private readonly IPetSocialService _svc;
    public PetSocialController(IPetSocialService svc) => _svc = svc;

    [HttpGet("{petId:guid}")]
    public Task<PetSocialProfileDto> Profile(Guid petId, CancellationToken ct) => _svc.GetProfileAsync(petId, ct);

    [HttpPost("{petId:guid}/follow")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Follow(Guid petId, CancellationToken ct) { await _svc.FollowAsync(petId, ct); return NoContent(); }

    [HttpDelete("{petId:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid petId, CancellationToken ct) { await _svc.UnfollowAsync(petId, ct); return NoContent(); }

    [HttpGet("followed/photos")]
    public Task<IReadOnlyList<FollowedPetPhotoDto>> FollowedPhotos([FromQuery] int take = 20, CancellationToken ct = default)
        => _svc.FollowedPhotosAsync(take, ct);

    [HttpPost("{petId:guid}/albums")]
    [EnableRateLimiting("writes")]
    public Task<PetAlbumDto> CreateAlbum(Guid petId, [FromBody] CreatePetAlbumRequest req, CancellationToken ct) => _svc.CreateAlbumAsync(petId, req, ct);

    [HttpPut("albums/{albumId:guid}")]
    public Task<PetAlbumDto> UpdateAlbum(Guid albumId, [FromBody] UpdatePetAlbumRequest req, CancellationToken ct) => _svc.UpdateAlbumAsync(albumId, req, ct);

    [HttpDelete("albums/{albumId:guid}")]
    public async Task<IActionResult> DeleteAlbum(Guid albumId, CancellationToken ct) { await _svc.DeleteAlbumAsync(albumId, ct); return NoContent(); }

    [HttpPost("albums/{albumId:guid}/photos")]
    [EnableRateLimiting("writes")]
    public Task<PetAlbumPhotoDto> AddPhoto(Guid albumId, [FromBody] AddAlbumPhotoRequest req, CancellationToken ct) => _svc.AddPhotoAsync(albumId, req, ct);

    [HttpDelete("albums/{albumId:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid albumId, Guid photoId, CancellationToken ct) { await _svc.DeletePhotoAsync(albumId, photoId, ct); return NoContent(); }
}
