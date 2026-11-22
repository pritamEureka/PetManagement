using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
