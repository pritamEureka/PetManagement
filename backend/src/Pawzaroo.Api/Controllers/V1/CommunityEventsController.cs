using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Event discovery (location-aware), group event feed, and RSVPs.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/community-events")]
[Authorize]
public class CommunityEventsController : ControllerBase
{
    private readonly ICommunityService _svc;
    public CommunityEventsController(ICommunityService svc) => _svc = svc;

    /// <summary>Upcoming events from public communities (+ the caller's private ones). Pass lat/lng/radiusKm for nearby search.</summary>
    [HttpGet]
    public Task<CommunityPage<CommunityEventDto>> Discover(
        [FromQuery] double? lat, [FromQuery] double? lng, [FromQuery] double? radiusKm,
        [FromQuery] string? city, [FromQuery] CommunityEventType? type,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        _svc.DiscoverEventsAsync(new EventDiscoveryQuery(lat, lng, radiusKm, city, type, from, to, page, pageSize), ct);

    /// <summary>Upcoming events from the communities the caller belongs to.</summary>
    [HttpGet("feed")]
    public Task<CommunityPage<CommunityEventDto>> Feed([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        _svc.GroupEventFeedAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<CommunityEventDto> Get(Guid id, CancellationToken ct) => _svc.GetEventAsync(id, ct);

    [HttpPost("{id:guid}/rsvp")]
    public Task<CommunityEventDto> Rsvp(Guid id, CancellationToken ct) => _svc.RsvpAsync(id, ct);

    [HttpDelete("{id:guid}/rsvp")]
    public Task<CommunityEventDto> CancelRsvp(Guid id, CancellationToken ct) => _svc.CancelRsvpAsync(id, ct);

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelEvent(Guid id, CancellationToken ct)
    {
        await _svc.CancelEventAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/attendees")]
    public Task<IReadOnlyList<EventAttendeeDto>> Attendees(Guid id, CancellationToken ct) => _svc.ListAttendeesAsync(id, ct);
}
