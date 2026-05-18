using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Messaging.Dtos;
using Pawzaroo.Application.Modules.Messaging.Services;

namespace Pawzaroo.Api.Controllers.V1;

public record StartConversationDto(Guid OtherUserId, string? ContextType, Guid? ContextRefId, string? InitialMessage);
public record SendMessageDto(string Type, string? Content, string? MediaUrl, Guid? ReplyToMessageId, List<AttachmentInputDto>? Attachments);
public record AttachmentInputDto(string Url, string MimeType, long SizeBytes, string? FileName, int? Width, int? Height);
public record MarkReadDto(Guid? LastMessageId);
public record ArchiveDto(bool Archived);
public record MuteDto(bool Muted);
public record ReportMessageDto(string Reason, string? Details);
public record BlockDto(Guid BlockedUserId, string? Reason);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessagingService _messaging;
    private readonly IMessageModerationService _moderation;

    public MessagesController(IMessagingService messaging, IMessageModerationService moderation)
    {
        _messaging = messaging;
        _moderation = moderation;
    }

    // ---------- Conversations ----------

    [HttpGet("conversations")]
    public Task<IReadOnlyList<ConversationSummaryDto>> Conversations(
        [FromQuery] bool includeArchived = false, [FromQuery] string? search = null, CancellationToken ct = default)
        => _messaging.ListConversationsAsync(includeArchived, search, ct);

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<ConversationSummaryDto>> Get(Guid id, CancellationToken ct)
    {
        var c = await _messaging.GetConversationAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("conversations")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Start([FromBody] StartConversationDto dto, CancellationToken ct)
    {
        var id = await _messaging.StartConversationAsync(
            new StartConversationInput(dto.OtherUserId, dto.ContextType, dto.ContextRefId, dto.InitialMessage), ct);
        return Ok(new { id });
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public Task<CursorPage<MessageDto>> History(Guid id, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _messaging.GetMessagesAsync(id, cursor, pageSize, ct);

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, [FromBody] MarkReadDto dto, CancellationToken ct)
    {
        var last = await _messaging.MarkReadAsync(id, dto.LastMessageId, ct);
        return Ok(new { lastMessageId = last });
    }

    [HttpPost("conversations/{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveDto dto, CancellationToken ct)
    {
        await _messaging.SetArchivedAsync(id, dto.Archived, ct);
        return NoContent();
    }

    [HttpPost("conversations/{id:guid}/mute")]
    public async Task<IActionResult> Mute(Guid id, [FromBody] MuteDto dto, CancellationToken ct)
    {
        await _messaging.SetMutedAsync(id, dto.Muted, ct);
        return NoContent();
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> Unread(CancellationToken ct) => Ok(new { count = await _messaging.GetTotalUnreadAsync(ct) });

    // ---------- Messages ----------

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting("writes")]
    public async Task<ActionResult<MessageDto>> Send(Guid id, [FromBody] SendMessageDto dto, CancellationToken ct)
    {
        var msg = await _messaging.SendMessageAsync(new SendMessageInput(
            id, dto.Type, dto.Content, dto.MediaUrl, dto.ReplyToMessageId,
            dto.Attachments?.Select(a => new AttachmentInput(a.Url, a.MimeType, a.SizeBytes, a.FileName, a.Width, a.Height)).ToList()), ct);

        // SignalR fan-out happens inside SendMessageAsync via the per-user
        // chat-hub group, so every participant's client gets the "message"
        // event without having to be in the conversation group.
        return Ok(msg);
    }

    [HttpDelete("messages/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _messaging.DeleteMessageAsync(id, ct);
        return NoContent();
    }

    [HttpPost("messages/{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, [FromBody] ReportMessageDto dto, CancellationToken ct)
    {
        await _moderation.ReportMessageAsync(id, new ReportMessageInput(dto.Reason, dto.Details), ct);
        return NoContent();
    }

    // ---------- Block ----------

    [HttpPost("block")]
    public async Task<IActionResult> Block([FromBody] BlockDto dto, CancellationToken ct)
    {
        await _moderation.BlockAsync(new BlockUserInput(dto.BlockedUserId, dto.Reason), ct);
        return NoContent();
    }

    [HttpPost("unblock/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        await _moderation.UnblockAsync(userId, ct);
        return NoContent();
    }

    [HttpGet("blocked")]
    public Task<IReadOnlyList<Guid>> Blocked(CancellationToken ct) => _moderation.ListBlockedAsync(ct);

    // ---------- Admin moderation ----------

    [HttpGet("admin/reported")]
    [Permission(Permissions.Messaging.Moderate)]
    public Task<IReadOnlyList<ReportedMessageDto>> ListReported([FromQuery] bool resolved = false, CancellationToken ct = default)
        => _moderation.ListReportedAsync(resolved, ct);

    [HttpPost("admin/reports/{reportId:guid}/resolve")]
    [Permission(Permissions.Messaging.Moderate)]
    public async Task<IActionResult> Resolve(Guid reportId, [FromQuery] bool delete = false, CancellationToken ct = default)
    {
        await _moderation.ResolveReportAsync(reportId, delete, ct);
        return NoContent();
    }
}
