using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Services;

namespace Pawzaroo.Api.Controllers.V1;

public record AddCommentDto(string Content, Guid? ParentCommentId);
public record EditCommentDto(string Content);
public record ReportCommentDto(string Reason, string? Details);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public class CommentsController : ControllerBase
{
    private readonly IFeedCommentService _comments;
    private readonly IFeedReportService _reports;

    public CommentsController(IFeedCommentService comments, IFeedReportService reports)
    {
        _comments = comments;
        _reports = reports;
    }

    [HttpGet("posts/{postId:guid}/comments")]
    [AllowAnonymous]
    public Task<CursorPage<CommentDto>> List(Guid postId,
        [FromQuery] string? cursor, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _comments.ListAsync(postId, cursor, pageSize, ct);

    [HttpPost("posts/{postId:guid}/comments")]
    [Authorize]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<CommentDto>> Add(Guid postId, [FromBody] AddCommentDto dto, CancellationToken ct)
    {
        var c = await _comments.AddAsync(postId, dto.Content, dto.ParentCommentId, ct);
        return Ok(c);
    }

    [HttpPut("comments/{id:guid}")]
    [Authorize]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<CommentDto>> Edit(Guid id, [FromBody] EditCommentDto dto, CancellationToken ct)
        => Ok(await _comments.EditAsync(id, dto.Content, ct));

    [HttpDelete("comments/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _comments.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("comments/{id:guid}/reports")]
    [Authorize]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportCommentDto dto, CancellationToken ct)
    {
        await _reports.ReportCommentAsync(id, dto.Reason, dto.Details, ct);
        return NoContent();
    }
}
