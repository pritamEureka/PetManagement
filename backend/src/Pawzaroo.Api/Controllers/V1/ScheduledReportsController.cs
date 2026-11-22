using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Intelligence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Scheduled CSV exports of the intelligence datasets. Read: users.view; write/run: users.edit.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/intelligence/schedules")]
[Authorize]
public class ScheduledReportsController : ControllerBase
{
    private readonly IScheduledReportService _svc;
    public ScheduledReportsController(IScheduledReportService svc) => _svc = svc;

    [HttpGet]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<ScheduledReportDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    [Permission(Permissions.Users.Edit)]
    public Task<ScheduledReportDto> Create([FromBody] SaveScheduledReportRequest body, CancellationToken ct) => _svc.CreateAsync(body, ct);

    [HttpPut("{id:guid}")]
    [Permission(Permissions.Users.Edit)]
    public Task<ScheduledReportDto> Update(Guid id, [FromBody] SaveScheduledReportRequest body, CancellationToken ct) => _svc.UpdateAsync(id, body, ct);

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.Users.Edit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    [Permission(Permissions.Users.Edit)]
    public Task<ReportRunDto> RunNow(Guid id, CancellationToken ct) => _svc.RunNowAsync(id, ct);

    [HttpGet("{id:guid}/runs")]
    [Permission(Permissions.Users.View)]
    public Task<IReadOnlyList<ReportRunDto>> Runs(Guid id, [FromQuery] int take = 20, CancellationToken ct = default) => _svc.ListRunsAsync(id, take, ct);

    [HttpGet("{id:guid}/runs/{runId:guid}/csv")]
    [Permission(Permissions.Users.View)]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> RunCsv(Guid id, Guid runId, CancellationToken ct)
    {
        var f = await _svc.GetRunCsvAsync(id, runId, ct);
        return f is null ? NotFound() : File(Encoding.UTF8.GetBytes(f.Csv), "text/csv", f.FileName);
    }
}
