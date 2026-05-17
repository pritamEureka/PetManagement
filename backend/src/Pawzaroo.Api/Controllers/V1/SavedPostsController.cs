using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/saved-posts")]
[Authorize]
public class SavedPostsController : ControllerBase
{
    private readonly IFeedSavedPostService _saved;

    public SavedPostsController(IFeedSavedPostService saved) => _saved = saved;

    [HttpGet]
    public Task<CursorPage<FeedItemDto>> List([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _saved.ListAsync(cursor, pageSize, ct);

    [HttpPost("{postId:guid}")]
    public async Task<IActionResult> Toggle(Guid postId, CancellationToken ct)
    {
        var nowSaved = await _saved.ToggleAsync(postId, ct);
        return Ok(new { saved = nowSaved });
    }
}
