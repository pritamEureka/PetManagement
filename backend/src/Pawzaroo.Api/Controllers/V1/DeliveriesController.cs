using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/deliveries")]
[Authorize]
public class DeliveriesController : ControllerBase
{
    private readonly IDeliveryService _deliveries;

    public DeliveriesController(IDeliveryService deliveries) { _deliveries = deliveries; }

    // -------- Admin --------------------------------------------------------------

    [HttpGet("admin/users")]
    public Task<IReadOnlyList<DeliveryUserSummaryDto>> ListDeliveryUsers(CancellationToken ct)
        => _deliveries.ListDeliveryUsersAsync(ct);

    [HttpGet("admin/assignments")]
    public Task<PageResult<DeliveryAssignmentDto>> AdminList(
        [FromQuery] DeliveryAssignmentStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => _deliveries.ListAdminAsync(status, page, pageSize, ct);

    [HttpPost("admin/orders/{orderId:guid}/assign")]
    public async Task<ActionResult<object>> Assign(Guid orderId, [FromBody] AssignDeliveryInput input, CancellationToken ct)
    {
        var id = await _deliveries.AssignAsync(orderId, input, ct);
        return Ok(new { id });
    }

    // -------- Delivery user ------------------------------------------------------

    [HttpGet("mine/active")]
    public Task<IReadOnlyList<DeliveryAssignmentDto>> MineActive(CancellationToken ct)
        => _deliveries.ListMineActiveAsync(ct);

    [HttpGet("mine/history")]
    public Task<PageResult<DeliveryAssignmentDto>> MineHistory(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
        => _deliveries.ListMineHistoryAsync(page, pageSize, ct);

    [HttpPut("{assignmentId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid assignmentId, [FromBody] UpdateDeliveryStatusInput input, CancellationToken ct)
    {
        await _deliveries.UpdateStatusAsync(assignmentId, input, ct);
        return NoContent();
    }
}
