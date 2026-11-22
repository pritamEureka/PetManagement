using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Modules.Push;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Web Push subscriptions and the public VAPID key. Config: Push:VapidPublicKey.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushSubscriptionService _svc;
    private readonly IConfiguration _config;

    public PushController(IPushSubscriptionService svc, IConfiguration config)
    {
        _svc = svc;
        _config = config;
    }

    /// <summary>Public by design (the key is not secret); PublicKey is null when push is not configured.</summary>
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public VapidKeyDto VapidPublicKey()
    {
        var key = _config["Push:VapidPublicKey"];
        return new VapidKeyDto(string.IsNullOrWhiteSpace(key) ? null : key);
    }

    [HttpGet("subscriptions")]
    public Task<IReadOnlyList<PushSubscriptionDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost("subscriptions")]
    public Task<PushSubscriptionDto> Subscribe([FromBody] PushSubscribeRequest req, CancellationToken ct) =>
        _svc.SubscribeAsync(req, ct);

    /// <summary>Unsubscribe by endpoint URL (query string, URL-encoded).</summary>
    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
    {
        await _svc.UnsubscribeAsync(endpoint, ct);
        return NoContent();
    }
}

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notification-preferences")]
[Authorize]
public class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferenceService _svc;
    public NotificationPreferencesController(INotificationPreferenceService svc) => _svc = svc;

    [HttpGet]
    public Task<NotificationPreferencesDto> Get(CancellationToken ct) => _svc.GetAsync(ct);

    [HttpPut]
    public Task<NotificationPreferencesDto> Put([FromBody] NotificationPreferencesDto dto, CancellationToken ct) =>
        _svc.PutAsync(dto, ct);
}

/// <summary>Mute controls: mute a category / conversation / community / source, optionally for N hours.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notification-mutes")]
[Authorize]
public class NotificationMutesController : ControllerBase
{
    private readonly INotificationMuteService _svc;
    public NotificationMutesController(INotificationMuteService svc) => _svc = svc;

    [HttpGet]
    public Task<IReadOnlyList<NotificationMuteDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    [EnableRateLimiting("writes")]
    public Task<NotificationMuteDto> Mute([FromBody] CreateNotificationMuteRequest req, CancellationToken ct) =>
        _svc.MuteAsync(req, ct);

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("writes")]
    public async Task<IActionResult> Unmute(Guid id, CancellationToken ct) =>
        await _svc.UnmuteAsync(id, ct) ? NoContent() : NotFound();
}

/// <summary>Per-category delivery frequency (Instant / Hourly / Daily digest).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notification-digest")]
[Authorize]
public class NotificationDigestController : ControllerBase
{
    private readonly INotificationMuteService _svc;
    public NotificationDigestController(INotificationMuteService svc) => _svc = svc;

    [HttpGet]
    public Task<IReadOnlyList<DigestPreferenceDto>> Get(CancellationToken ct) => _svc.GetDigestAsync(ct);

    [HttpPut]
    [EnableRateLimiting("writes")]
    public Task<IReadOnlyList<DigestPreferenceDto>> Put([FromBody] List<DigestPreferenceDto> prefs, CancellationToken ct) =>
        _svc.PutDigestAsync(prefs, ct);
}
