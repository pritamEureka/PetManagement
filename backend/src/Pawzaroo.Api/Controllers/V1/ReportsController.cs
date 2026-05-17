using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Moderation;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>File an abuse report. Any authenticated user can call this.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] ReportContentInput input, CancellationToken ct)
    {
        var id = await _reports.CreateAsync(input, ct);
        return Ok(new { id });
    }

    [HttpGet]
    [Permission(Permissions.Moderation.View)]
    public Task<IReadOnlyList<ContentReportDto>> List(
        [FromQuery] ReportStatus? status,
        [FromQuery] ReportTargetType? targetType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _reports.ListAsync(status, targetType, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [Permission(Permissions.Moderation.View)]
    public async Task<ActionResult<ContentReportDto>> Get(Guid id, CancellationToken ct)
    {
        var r = await _reports.GetAsync(id, ct);
        return r is null ? NotFound() : Ok(r);
    }

    public record SetStatusBody(ReportStatus Status, string? Notes);

    [HttpPut("{id:guid}/status")]
    [Permission(Permissions.Moderation.Moderate)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetStatusBody body, CancellationToken ct)
    {
        await _reports.SetStatusAsync(id, body.Status, body.Notes, ct);
        return NoContent();
    }
}
