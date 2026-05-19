using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IReturnService _returns;

    public OrdersController(IOrderService orders, IReturnService returns)
    {
        _orders = orders;
        _returns = returns;
    }

    [HttpPost("checkout")]
    public Task<OrderDto> Checkout([FromBody] CheckoutInput input, CancellationToken ct) => _orders.CheckoutAsync(input, ct);

    [HttpGet("mine")]
    public Task<PageResult<OrderDto>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _orders.ListMineAsync(page, pageSize, ct);

    [HttpGet("store/{storeId:guid}")]
    public Task<PageResult<OrderDto>> ForStore(Guid storeId,
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _orders.ListForStoreAsync(storeId, status, page, pageSize, ct);

    [HttpGet("admin/all")]
    [Permission(Permissions.Orders.View)]
    public Task<PageResult<OrderDto>> AdminAll(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _orders.ListForAdminAsync(status, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
    {
        var o = await _orders.GetByIdAsync(id, ct);
        return o is null ? NotFound() : Ok(o);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusInput input, CancellationToken ct)
    {
        await _orders.UpdateStatusAsync(id, input.Status, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/shipment")]
    public async Task<IActionResult> UpdateShipment(Guid id, [FromBody] UpdateShipmentStatusInput input, CancellationToken ct)
    {
        await _orders.UpdateShipmentStatusAsync(id, input.Status, input.TrackingNumber, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderBody? body, CancellationToken ct)
    {
        await _orders.CancelAsync(id, body?.Reason, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deny")]
    public async Task<IActionResult> Deny(Guid id, [FromBody] CancelOrderBody? body, CancellationToken ct)
    {
        await _orders.DenyAsync(id, body?.Reason, ct);
        return NoContent();
    }

    /// <summary>
    /// Server-rendered printable invoice. Returns text/html so the browser can
    /// open in a new tab and use the native Print → Save as PDF path — avoids
    /// pulling in a PDF library for what's essentially a styled receipt.
    /// </summary>
    [HttpGet("{id:guid}/invoice")]
    [Produces("text/html")]
    public async Task<IActionResult> Invoice(Guid id, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(id, ct);
        if (order is null) return NotFound();
        return Content(InvoiceRenderer.Render(order), "text/html");
    }

    [HttpPost("{id:guid}/refund")]
    [Permission(Permissions.Orders.Refund)]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundBody? body, CancellationToken ct)
    {
        await _orders.RefundAsync(id, body?.Amount, ct);
        return NoContent();
    }

    // ----- Returns ------------------------------------------------------------

    [HttpPost("returns")]
    public Task<ReturnRequestDto> CreateReturn([FromBody] CreateReturnRequestInput input, CancellationToken ct)
        => _returns.CreateAsync(input, ct);

    [HttpGet("returns")]
    [Permission(Permissions.Orders.View)]
    public Task<PageResult<ReturnRequestDto>> ListReturns(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _returns.ListAsync(page, pageSize, ct);

    [HttpPost("returns/{id:guid}/approve")]
    [Permission(Permissions.Orders.Refund)]
    public async Task<IActionResult> ApproveReturn(Guid id, [FromBody] RefundBody? body, CancellationToken ct)
    {
        await _returns.ApproveAsync(id, body?.Amount, ct);
        return NoContent();
    }

    [HttpPost("returns/{id:guid}/reject")]
    [Permission(Permissions.Orders.Refund)]
    public async Task<IActionResult> RejectReturn(Guid id, [FromBody] RejectReturnBody? body, CancellationToken ct)
    {
        await _returns.RejectAsync(id, body?.Notes, ct);
        return NoContent();
    }

    public record CancelOrderBody(string? Reason);
    public record RefundBody(decimal? Amount);
    public record RejectReturnBody(string? Notes);
}
