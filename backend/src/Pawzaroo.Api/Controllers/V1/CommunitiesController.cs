using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Pet communities: browse, create, join, moderate members, and host events inside a group.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/communities")]
[Authorize]
public class CommunitiesController : ControllerBase
{
    private readonly ICommunityService _svc;
    public CommunitiesController(ICommunityService svc) => _svc = svc;

    [HttpGet]
    public Task<CommunityPage<CommunityDto>> List(
        [FromQuery] string? q, [FromQuery] string? species, [FromQuery] string? city,
        [FromQuery] bool mine = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        _svc.ListAsync(new CommunityQuery(q, species, city, mine, page, pageSize), ct);

    /// <summary>Accepts a community id (guid) or slug.</summary>
    [HttpGet("{idOrSlug}")]
    public Task<CommunityDto> Get(string idOrSlug, CancellationToken ct) => _svc.GetAsync(idOrSlug, ct);

    [HttpPost]
    public Task<CommunityDto> Create([FromBody] CreateCommunityRequest req, CancellationToken ct) => _svc.CreateAsync(req, ct);

    [HttpPost("{id:guid}/join")]
    public Task<CommunityDto> Join(Guid id, CancellationToken ct) => _svc.JoinAsync(id, ct);

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await _svc.LeaveAsync(id, ct);
        return NoContent();
    }

    /// <summary>Members (Active by default). Pending/Banned lists are moderators-only.</summary>
    [HttpGet("{id:guid}/members")]
    public Task<IReadOnlyList<CommunityMemberDto>> Members(Guid id, [FromQuery] CommunityMemberStatus? status, CancellationToken ct) =>
        _svc.ListMembersAsync(id, status, ct);

    /// <summary>action: Approve | Ban | Unban | Promote | Demote | Remove.</summary>
    [HttpPost("{id:guid}/members/{userId:guid}/{action}")]
    public async Task<IActionResult> Moderate(Guid id, Guid userId, MemberAction action, CancellationToken ct)
    {
        await _svc.ModerateAsync(id, userId, action, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/events")]
    public Task<IReadOnlyList<CommunityEventDto>> Events(Guid id, [FromQuery] bool includePast = false, CancellationToken ct = default) =>
        _svc.ListCommunityEventsAsync(id, includePast, ct);

    [HttpPost("{id:guid}/events")]
    public Task<CommunityEventDto> CreateEvent(Guid id, [FromBody] CreateCommunityEventRequest req, CancellationToken ct) =>
        _svc.CreateEventAsync(id, req, ct);
}
