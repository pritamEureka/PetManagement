using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Infrastructure.Messaging;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Inspect, re-drive or discard messages parked on dead-letter topics.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dead-letters")]
[Authorize]
public class AdminDeadLettersController : ControllerBase
{
    private readonly IDeadLetterService _svc;
    private readonly ICurrentUserService _me;

    public AdminDeadLettersController(IDeadLetterService svc, ICurrentUserService me)
    {
        _svc = svc;
        _me = me;
    }

    [HttpGet]
    [Permission(Permissions.Audit.View)]
    public async Task<DeadLetterPage> List([FromQuery] string? status, [FromQuery] string? topic,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        DeadLetterStatus? parsed = Enum.TryParse<DeadLetterStatus>(status, true, out var s) ? s : null;
        return await _svc.ListAsync(parsed, topic, page, pageSize, ct);
    }

    [HttpGet("{id:guid}")]
    [Permission(Permissions.Audit.View)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => await _svc.GetAsync(id, ct) is { } d ? Ok(d) : NotFound();

    [HttpPost("{id:guid}/redrive")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Redrive(Guid id, CancellationToken ct)
        => await _svc.RedriveAsync(id, _me.UserId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/discard")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
        => await _svc.DiscardAsync(id, _me.UserId, ct) ? NoContent() : NotFound();
}
