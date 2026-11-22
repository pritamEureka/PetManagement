using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Modules.Messaging;

namespace Pawzaroo.Api.Controllers.V1;

public record AdvanceStageDto(AdoptionApplicationStage Stage, string? Note);
public record AddReferenceDto(string Name, string Contact, string? Relationship);
public record UpdateReferenceDto(AdoptionReferenceStatus Status, string? Notes);
public record ScheduleVisitDto(DateTime ScheduledAt, string? Notes);
public record UpdateVisitDto(AdoptionHomeVisitStatus Status, string? Notes, string? Outcome);
public record CompleteFollowUpDto(int WelfareScore, string? Notes);
public record StartFosterDto(Guid FosterUserId, string? Notes);
public record EndFosterDto(FosterOutcome Outcome, string? Notes);
public record MatchRequestDto(List<Guid> ListingIds);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/adoption-plus")]
[Authorize]
public class AdoptionPlusController : ControllerBase
{
    private readonly IAdoptionPlusService _svc;
    public AdoptionPlusController(IAdoptionPlusService svc) => _svc = svc;

    [HttpGet("pipeline")] public Task<IReadOnlyList<PipelineItemDto>> Pipeline([FromQuery] Guid? listingId, CancellationToken ct) => _svc.GetPipelineAsync(listingId, ct);
    [HttpGet("my-applications")] public Task<IReadOnlyList<MyApplicationDto>> Mine(CancellationToken ct) => _svc.GetMyApplicationsAsync(ct);
    [HttpGet("requests/{id:guid}/timeline")] public Task<IReadOnlyList<StageEventDto>> Timeline(Guid id, CancellationToken ct) => _svc.GetTimelineAsync(id, ct);

    [HttpPost("requests/{id:guid}/stage")]
    public async Task<IActionResult> Advance(Guid id, [FromBody] AdvanceStageDto dto, CancellationToken ct) { await _svc.AdvanceAsync(id, dto.Stage, dto.Note, ct); return NoContent(); }

    [HttpGet("requests/{id:guid}/references")] public Task<IReadOnlyList<ReferenceDto>> References(Guid id, CancellationToken ct) => _svc.ListReferencesAsync(id, ct);
    [HttpPost("requests/{id:guid}/references")] [EnableRateLimiting("writes")]
    public Task<ReferenceDto> AddReference(Guid id, [FromBody] AddReferenceDto d, CancellationToken ct) => _svc.AddReferenceAsync(id, d.Name, d.Contact, d.Relationship, ct);
    [HttpPut("references/{id:guid}")] public Task<ReferenceDto> UpdateReference(Guid id, [FromBody] UpdateReferenceDto d, CancellationToken ct) => _svc.UpdateReferenceAsync(id, d.Status, d.Notes, ct);

    [HttpGet("requests/{id:guid}/home-visits")] public Task<IReadOnlyList<HomeVisitDto>> Visits(Guid id, CancellationToken ct) => _svc.ListHomeVisitsAsync(id, ct);
    [HttpPost("requests/{id:guid}/home-visits")] public Task<HomeVisitDto> Schedule(Guid id, [FromBody] ScheduleVisitDto d, CancellationToken ct) => _svc.ScheduleHomeVisitAsync(id, d.ScheduledAt, d.Notes, ct);
    [HttpPut("home-visits/{id:guid}")] public Task<HomeVisitDto> UpdateVisit(Guid id, [FromBody] UpdateVisitDto d, CancellationToken ct) => _svc.UpdateHomeVisitAsync(id, d.Status, d.Notes, d.Outcome, ct);

    [HttpGet("follow-ups/mine")] public Task<IReadOnlyList<FollowUpDto>> MyFollowUps(CancellationToken ct) => _svc.ListMyFollowUpsAsync(ct);
    [HttpGet("listings/{id:guid}/follow-ups")] public Task<IReadOnlyList<FollowUpDto>> ListingFollowUps(Guid id, CancellationToken ct) => _svc.ListFollowUpsForListingAsync(id, ct);
    [HttpGet("listings/{id:guid}/outcomes")] public Task<FollowUpSummaryDto> Outcomes(Guid id, CancellationToken ct) => _svc.GetOutcomeSummaryAsync(id, ct);
    [HttpPost("follow-ups/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteFollowUpDto d, CancellationToken ct) { await _svc.CompleteFollowUpAsync(id, d.WelfareScore, d.Notes, ct); return NoContent(); }

    [HttpGet("listings/{id:guid}/fosters")] public Task<IReadOnlyList<FosterDto>> Fosters(Guid id, CancellationToken ct) => _svc.ListFostersAsync(id, ct);
    [HttpPost("listings/{id:guid}/fosters")] public Task<FosterDto> StartFoster(Guid id, [FromBody] StartFosterDto d, CancellationToken ct) => _svc.StartFosterAsync(id, d.FosterUserId, d.Notes, ct);
    [HttpPost("fosters/{id:guid}/end")]
    public async Task<IActionResult> EndFoster(Guid id, [FromBody] EndFosterDto d, CancellationToken ct) { await _svc.EndFosterAsync(id, d.Outcome, d.Notes, ct); return NoContent(); }

    [HttpGet("profile")]
    public async Task<ActionResult<AdopterProfileDto>> Profile(CancellationToken ct) { var p = await _svc.GetProfileAsync(ct); return p is null ? NoContent() : Ok(p); }
    [HttpPut("profile")] public Task<AdopterProfileDto> UpsertProfile([FromBody] AdopterProfileDto d, CancellationToken ct) => _svc.UpsertProfileAsync(d, ct);
    [HttpPost("match")] public Task<IReadOnlyList<MatchDto>> Match([FromBody] MatchRequestDto d, CancellationToken ct) => _svc.MatchListingsAsync(d.ListingIds ?? new(), ct);

    /// <summary>Admin approval page: risk flags for a set of listings (requires adoption approve/reject permission, checked in service).</summary>
    [HttpPost("risk-flags")] public Task<IReadOnlyList<RiskFlagDto>> Risk([FromBody] MatchRequestDto d, CancellationToken ct) => _svc.GetRiskFlagsAsync(d.ListingIds ?? new(), ct);
}

public record SearchMsgQuery(string Q);
public record PinDto(Guid MessageId);
public record FolderCreateDto(string Name);
public record FolderItemDto(Guid ConversationId, bool InFolder);
public record DisappearDto(int? Seconds);
public record E2eeToggleDto(bool Enabled);
public record EditMessageDto(string Content);
public record RegisterKeyDto(string PublicKeyJwk);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messaging-plus")]
[Authorize]
public class MessagingPlusController : ControllerBase
{
    private readonly IMessagingPlusService _svc;
    public MessagingPlusController(IMessagingPlusService svc) => _svc = svc;

    [HttpGet("search")] [EnableRateLimiting("writes")]
    public Task<MessageSearchPage> Search([FromQuery] string q, [FromQuery] Guid? conversationId, [FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _svc.SearchAsync(q, conversationId, cursor, pageSize, ct);

    [HttpGet("conversations/{id:guid}/pins")] public Task<IReadOnlyList<PinnedMessageDto>> Pins(Guid id, CancellationToken ct) => _svc.ListPinsAsync(id, ct);
    [HttpPost("messages/{id:guid}/pin")] public async Task<IActionResult> Pin(Guid id, CancellationToken ct) { await _svc.PinAsync(id, ct); return NoContent(); }
    [HttpDelete("messages/{id:guid}/pin")] public async Task<IActionResult> Unpin(Guid id, CancellationToken ct) { await _svc.UnpinAsync(id, ct); return NoContent(); }

    [HttpGet("folders")] public Task<IReadOnlyList<FolderDto>> Folders(CancellationToken ct) => _svc.ListFoldersAsync(ct);
    [HttpPost("folders")] public Task<FolderDto> CreateFolder([FromBody] FolderCreateDto d, CancellationToken ct) => _svc.CreateFolderAsync(d.Name, ct);
    [HttpPut("folders/{id:guid}")] public Task<FolderDto> RenameFolder(Guid id, [FromBody] FolderCreateDto d, CancellationToken ct) => _svc.RenameFolderAsync(id, d.Name, ct);
    [HttpDelete("folders/{id:guid}")] public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken ct) { await _svc.DeleteFolderAsync(id, ct); return NoContent(); }
    [HttpPut("folders/{id:guid}/items")] public async Task<IActionResult> FolderItem(Guid id, [FromBody] FolderItemDto d, CancellationToken ct) { await _svc.SetFolderItemAsync(id, d.ConversationId, d.InFolder, ct); return NoContent(); }

    [HttpGet("conversations/{id:guid}/settings")] public Task<ConversationSettingsDto> Settings(Guid id, CancellationToken ct) => _svc.GetSettingsAsync(id, ct);
    [HttpPut("conversations/{id:guid}/disappear")] public Task<ConversationSettingsDto> Disappear(Guid id, [FromBody] DisappearDto d, CancellationToken ct) => _svc.SetDisappearAsync(id, d.Seconds, ct);
    [HttpPut("conversations/{id:guid}/e2ee")] public Task<ConversationSettingsDto> E2ee(Guid id, [FromBody] E2eeToggleDto d, CancellationToken ct) => _svc.SetE2eeAsync(id, d.Enabled, ct);

    [HttpPut("messages/{id:guid}")] public async Task<IActionResult> Edit(Guid id, [FromBody] EditMessageDto d, CancellationToken ct) { await _svc.EditAsync(id, d.Content, ct); return NoContent(); }
    [HttpPost("messages/{id:guid}/recall")] public async Task<IActionResult> Recall(Guid id, CancellationToken ct) { await _svc.RecallAsync(id, ct); return NoContent(); }

    [HttpPost("e2ee/keys")] public Task<E2eeKeyDto> RegisterKey([FromBody] RegisterKeyDto d, CancellationToken ct) => _svc.RegisterKeyAsync(d.PublicKeyJwk, ct);
    [HttpDelete("e2ee/keys/{id:guid}")] public async Task<IActionResult> RevokeKey(Guid id, CancellationToken ct) { await _svc.RevokeKeyAsync(id, ct); return NoContent(); }
    [HttpGet("e2ee/users/{userId:guid}/keys")] public Task<IReadOnlyList<E2eeKeyDto>> Keys(Guid userId, CancellationToken ct) => _svc.GetKeysAsync(userId, ct);
}
