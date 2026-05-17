using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Services;

namespace Pawzaroo.Api.Controllers.V1;

public record CreatePostDto(string? Content, string? AnimalType, string? Location,
                            List<string>? MediaUrls, List<string>? Hashtags, List<Guid>? PetTagIds);
public record UpdatePostDto(string? Content, string? AnimalType, string? Location, List<string>? Hashtags);
public record ReactionDto(string Type);
public record ShareDto(string? Note);
public record ReportDto(string Reason, string? Details);
public record HideDto(bool Hidden, string? Reason);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/posts")]
public class PostsController : ControllerBase
{
    private readonly IFeedService _feed;
    private readonly IFeedReactionService _reactions;
    private readonly IFeedShareService _shares;
    private readonly IFeedReportService _reports;

    public PostsController(IFeedService feed, IFeedReactionService reactions, IFeedShareService shares, IFeedReportService reports)
    {
        _feed = feed;
        _reactions = reactions;
        _shares = shares;
        _reports = reports;
    }

    // ---------- Feeds ----------

    /// <summary>Public feed. AllowAnonymous so the landing page can preview.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<CursorPage<FeedItemDto>> Public(
        [FromQuery] string? cursor, [FromQuery] int pageSize = 20,
        [FromQuery] string? animalType = null, [FromQuery] string? hashtag = null,
        CancellationToken ct = default)
        => _feed.GetFeedAsync(new FeedQuery(FeedScope.Public, cursor, pageSize, animalType, hashtag), ct);

    [HttpGet("following")]
    [Authorize]
    public Task<CursorPage<FeedItemDto>> Following([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _feed.GetFeedAsync(new FeedQuery(FeedScope.Following, cursor, pageSize), ct);

    [HttpGet("mine")]
    [Authorize]
    public Task<CursorPage<FeedItemDto>> Mine([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _feed.GetFeedAsync(new FeedQuery(FeedScope.Mine, cursor, pageSize), ct);

    [HttpGet("user/{userId:guid}")]
    [AllowAnonymous]
    public Task<CursorPage<FeedItemDto>> ByUser(Guid userId, [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _feed.GetFeedAsync(new FeedQuery(FeedScope.User, cursor, pageSize, UserId: userId), ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<FeedItemDto>> Get(Guid id, CancellationToken ct)
    {
        var post = await _feed.GetByIdAsync(id, ct);
        return post is null ? NotFound() : Ok(post);
    }

    // ---------- Mutations ----------

    [HttpPost]
    [Authorize]
    [Permission(Permissions.Posts.Create)]
    [EnableRateLimiting("writes")]
    [Audit("Feed", "create", entityName: "Post", entityIdRouteKey: null)]
    public async Task<IActionResult> Create([FromBody] CreatePostDto dto, CancellationToken ct)
    {
        var id = await _feed.CreateAsync(new CreatePostInput(
            dto.Content, dto.AnimalType, dto.Location, dto.MediaUrls, dto.Hashtags, dto.PetTagIds), ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [EnableRateLimiting("writes")]
    [Audit("Feed", "update", entityName: "Post")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostDto dto, CancellationToken ct)
    {
        await _feed.UpdateAsync(id, new UpdatePostInput(dto.Content, dto.AnimalType, dto.Location, dto.Hashtags), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [Audit("Feed", "delete", entityName: "Post")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _feed.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/hide")]
    [Authorize]
    [Permission(Permissions.Posts.Moderate)]
    [Audit("Feed", "moderate", entityName: "Post")]
    public async Task<IActionResult> Hide(Guid id, [FromBody] HideDto dto, CancellationToken ct)
    {
        await _feed.SetHiddenAsync(id, dto.Hidden, dto.Reason, ct);
        return NoContent();
    }

    // ---------- Reactions ----------

    [HttpPost("{id:guid}/reactions")]
    [Authorize]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> React(Guid id, [FromBody] ReactionDto dto, CancellationToken ct)
    {
        await _reactions.SetReactionAsync(id, dto.Type, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/reactions")]
    [Authorize]
    public async Task<IActionResult> Unreact(Guid id, CancellationToken ct)
    {
        await _reactions.RemoveReactionAsync(id, ct);
        return NoContent();
    }

    // ---------- Share + report ----------

    [HttpPost("{id:guid}/shares")]
    [Authorize]
    public async Task<IActionResult> Share(Guid id, [FromBody] ShareDto dto, CancellationToken ct)
    {
        await _shares.ShareAsync(id, dto.Note, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reports")]
    [Authorize]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportDto dto, CancellationToken ct)
    {
        await _reports.ReportPostAsync(id, dto.Reason, dto.Details, ct);
        return NoContent();
    }
}
