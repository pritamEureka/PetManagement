using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/moderation")]
[Authorize]
public class ModerationController : ControllerBase
{
    private readonly IModerationService _moderation;
    private readonly IUserDisciplineService _discipline;
    private readonly IAdminActionLogger _adminLog;

    public ModerationController(IModerationService moderation,
        IUserDisciplineService discipline, IAdminActionLogger adminLog)
    {
        _moderation = moderation;
        _discipline = discipline;
        _adminLog = adminLog;
    }

    [HttpPost("actions")]
    [Permission(Permissions.Moderation.Moderate)]
    public Task<ModerationActionDto> Act([FromBody] ModerationActionInput input, CancellationToken ct)
        => _moderation.TakeActionAsync(input, ct);

    [HttpGet("targets/{targetType}/{targetId:guid}/history")]
    [Permission(Permissions.Moderation.View)]
    public Task<IReadOnlyList<ModerationActionDto>> History(
        ModerationTargetType targetType, Guid targetId, CancellationToken ct)
        => _moderation.HistoryAsync(targetType, targetId, ct);

    // --- User discipline ---------------------------------------------------

    public record SuspendBody(string Reason, string? Details, DateTime? ExpiresAt, bool IsBan = false);
    public record LiftBody(string? Notes);
    public record WarnBody(WarningSeverity Severity, string Reason, string? Message,
                           string? RelatedContentType, Guid? RelatedContentId);

    [HttpPost("users/{userId:guid}/suspend")]
    [Permission(Permissions.Users.Suspend)]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendBody body, CancellationToken ct)
    {
        var hold = await _discipline.SuspendAsync(userId, body.Reason, body.Details, body.ExpiresAt, body.IsBan, ct);
        return Ok(new { suspensionId = hold.Id });
    }

    [HttpPost("suspensions/{id:guid}/lift")]
    [Permission(Permissions.Users.Restore)]
    public async Task<IActionResult> Lift(Guid id, [FromBody] LiftBody body, CancellationToken ct)
    {
        await _discipline.LiftAsync(id, body.Notes, ct);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/warn")]
    [Permission(Permissions.Users.Suspend)]
    public async Task<IActionResult> Warn(Guid userId, [FromBody] WarnBody body, CancellationToken ct)
    {
        var w = await _discipline.WarnAsync(userId, body.Severity, body.Reason, body.Message,
            body.RelatedContentType, body.RelatedContentId, ct);
        return Ok(new { warningId = w.Id });
    }

    [HttpGet("users/{userId:guid}/warnings")]
    [Permission(Permissions.Moderation.View)]
    public Task<IReadOnlyList<UserWarningDto>> ListWarnings(Guid userId, CancellationToken ct)
        => _discipline.ListWarningsForUserAsync(userId, ct);

    [HttpGet("users/{userId:guid}/active-suspension")]
    [Permission(Permissions.Moderation.View)]
    public async Task<ActionResult<UserSuspensionDto>> ActiveSuspension(Guid userId, CancellationToken ct)
    {
        var s = await _discipline.GetActiveAsync(userId, ct);
        return s is null ? NotFound() : Ok(s);
    }

    // --- Admin action log --------------------------------------------------

    [HttpGet("admin-actions")]
    [Permission(Permissions.Audit.View)]
    public Task<IReadOnlyList<AdminActionLogDto>> AdminActions(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _adminLog.ListAsync(page, pageSize, ct);
}
