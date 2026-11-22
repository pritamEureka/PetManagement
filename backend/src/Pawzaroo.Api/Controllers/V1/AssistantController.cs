using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Assistant;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Care and marketplace assistant. Any signed-in user; conversations are private to their owner.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistant")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _svc;
    private readonly IAssistantTriageService _triage;
    public AssistantController(IAssistantService svc, IAssistantTriageService triage)
    {
        _svc = svc;
        _triage = triage;
    }

    [HttpPost("message")]
    public Task<AssistantExchangeDto> Send([FromBody] SendAssistantMessageRequest body, CancellationToken ct)
        => _svc.SendAsync(body, ct);

    [HttpGet("conversations")]
    public Task<IReadOnlyList<AssistantConversationSummaryDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<AssistantConversationDto> Get(Guid id, CancellationToken ct) => _svc.GetAsync(id, ct);

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Structured symptom checklist (+ optional uploaded photo URLs). Deterministic urgency; always queues a human vet review.</summary>
    [HttpPost("triage")]
    public Task<TriageResultDto> Triage([FromBody] SubmitTriageRequest body, CancellationToken ct) => _triage.SubmitAsync(body, ct);

    /// <summary>The caller's own review requests, including vet answers.</summary>
    [HttpGet("reviews/mine")]
    public Task<IReadOnlyList<ReviewRequestDto>> MyReviews(CancellationToken ct) => _triage.ListMyReviewsAsync(ct);

    /// <summary>Vet queue (vets.edit). status: Pending | Reviewed.</summary>
    [HttpGet("reviews")]
    [Permission(Permissions.Vets.Edit)]
    public Task<IReadOnlyList<ReviewRequestDto>> Reviews([FromQuery] string? status = "Pending", [FromQuery] int take = 50, CancellationToken ct = default)
        => _triage.ListReviewsAsync(status, take, ct);

    /// <summary>Answer a review request (vets.edit).</summary>
    [HttpPost("reviews/{id:guid}/answer")]
    [Permission(Permissions.Vets.Edit)]
    public Task<ReviewRequestDto> Answer(Guid id, [FromBody] AnswerReviewRequest body, CancellationToken ct) => _triage.AnswerReviewAsync(id, body, ct);
}
