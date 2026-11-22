using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Filters;

namespace Pawzaroo.Tests.Integration.Api;

/// <summary>
/// TEST-ONLY endpoint carrying [Idempotent] (the real ones are heavy booking flows). Registered through an
/// ApplicationPart in <see cref="ApiFactory"/>; it does not exist in the production app.
/// </summary>
[ApiController]
[Route("test-support/idempotent")]
[Authorize]
public sealed class IdempotencyProbeController : ControllerBase
{
    public static int Executions;
    public sealed record ProbeInput(string Value);

    [HttpPost]
    [Idempotent]
    public IActionResult Post([FromBody] ProbeInput input)
        => Ok(new { run = Interlocked.Increment(ref Executions), echo = input.Value });
}
