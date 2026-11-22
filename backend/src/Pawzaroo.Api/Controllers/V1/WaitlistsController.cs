using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.VetPlus;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/waitlists")]
[Authorize]
public class WaitlistsController : ControllerBase
{
    private readonly IWaitlistService _svc;
    public WaitlistsController(IWaitlistService svc) => _svc = svc;

    [HttpPost]
    public Task<WaitlistEntryDto> Join([FromBody] JoinWaitlistInput input, CancellationToken ct) => _svc.JoinAsync(input, ct);

    [HttpGet("mine")]
    public Task<IReadOnlyList<WaitlistEntryDto>> Mine(CancellationToken ct) => _svc.MineAsync(ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await _svc.LeaveAsync(id, ct);
        return NoContent();
    }
}
