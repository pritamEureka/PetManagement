using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.SecurityPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Detected suspicious-activity signals (impossible travel, OTP brute force, enumeration).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/security-signals")]
[Authorize]
public class AdminSecuritySignalsController : ControllerBase
{
    private readonly ISecuritySignalService _signals;
    public AdminSecuritySignalsController(ISecuritySignalService signals) => _signals = signals;

    [HttpGet]
    [Permission(Permissions.Audit.View)]
    public Task<IReadOnlyList<SecuritySignalDto>> List(
        [FromQuery] string? type, [FromQuery] string? severity,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _signals.ListAsync(type, severity, page, pageSize, ct);
}
