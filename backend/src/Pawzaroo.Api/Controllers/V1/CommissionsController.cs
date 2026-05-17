using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/commissions")]
public class CommissionsController : ControllerBase
{
    private readonly ICommissionConfigurationService _svc;
    public CommissionsController(ICommissionConfigurationService svc) => _svc = svc;

    [HttpGet]
    [Permission(Permissions.Settings.View)]
    public Task<IReadOnlyList<CommissionConfigurationDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Create([FromBody] UpsertCommissionConfigurationInput input, CancellationToken ct)
    {
        var id = await _svc.UpsertAsync(input, ct);
        return Ok(new { id });
    }

    [HttpDelete("{id:guid}")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
