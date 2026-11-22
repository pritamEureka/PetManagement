using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.CommercePlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Order tracking timeline. Authorization (owner / store / assigned courier / admin) is enforced in the service.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tracking")]
[Authorize]
public class DeliveryTrackingController : ControllerBase
{
    private readonly IDeliveryTrackingService _tracking;
    public DeliveryTrackingController(IDeliveryTrackingService tracking) => _tracking = tracking;

    [HttpGet("orders/{orderId:guid}")]
    public Task<OrderTrackingDto> Timeline(Guid orderId, CancellationToken ct) => _tracking.GetTimelineAsync(orderId, ct);

    [HttpPost("orders/{orderId:guid}/checkpoints")]
    public async Task<ActionResult<object>> AddCheckpoint(Guid orderId, [FromBody] AddCheckpointInput input, CancellationToken ct)
    {
        var id = await _tracking.AddCheckpointAsync(orderId, input, ct);
        return Ok(new { id });
    }

    [HttpPut("orders/{orderId:guid}/proof")]
    public async Task<IActionResult> AddProof(Guid orderId, [FromBody] AddDeliveryProofInput input, CancellationToken ct)
    {
        await _tracking.AddProofAsync(orderId, input, ct);
        return NoContent();
    }
}
