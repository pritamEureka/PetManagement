using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Anonymous surfaces: the QR-tag profile (owner-approved fields only) and
/// time-limited health-record share links. Both are opaque-token lookups and
/// rate limited per IP.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public")]
[AllowAnonymous]
public class PublicPetsController : ControllerBase
{
    private readonly IPetSharingService _sharing;

    public PublicPetsController(IPetSharingService sharing) => _sharing = sharing;

    [HttpGet("pets/{code}")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<PublicPetDto>> Pet(string code, CancellationToken ct)
    {
        var pet = await _sharing.GetPublicPetAsync(code, ct);
        return pet is null ? NotFound() : Ok(pet);
    }

    [HttpPost("pets/{code}/sightings")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Sighting(string code, [FromBody] SightingInput input, CancellationToken ct)
    {
        await _sharing.ReportSightingAsync(code, input, ct);
        return NoContent();
    }

    [HttpGet("health/{token}")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<HealthReportDto>> SharedHealth(string token, CancellationToken ct)
    {
        var report = await _sharing.GetSharedHealthReportAsync(token, ct);
        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("health/{token}/report")]
    [EnableRateLimiting("auth")]
    [Produces("text/html")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> SharedHealthHtml(string token, CancellationToken ct)
    {
        var report = await _sharing.GetSharedHealthReportAsync(token, ct);
        if (report is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Content(PetHealthReportRenderer.Render(report), "text/html");
    }
}
