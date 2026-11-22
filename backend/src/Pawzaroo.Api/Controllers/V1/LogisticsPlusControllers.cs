using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Shipping quote (any signed-in user) plus admin CRUD for zones and peak rules (permission checks live in the service).</summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class ShippingController : ControllerBase
{
    private readonly IShippingQuoteService _quote;
    private readonly IDeliveryZoneAdminService _admin;
    public ShippingController(IShippingQuoteService quote, IDeliveryZoneAdminService admin) { _quote = quote; _admin = admin; }

    [HttpGet("api/v{version:apiVersion}/shipping/quote")]
    public Task<ShippingQuoteDto> Quote([FromQuery] Guid? storeId, [FromQuery] string? city, [FromQuery] decimal subtotal, CancellationToken ct)
        => _quote.QuoteAsync(storeId, city, subtotal, DateTime.UtcNow, ct);

    [HttpGet("api/v{version:apiVersion}/admin/delivery-zones")]
    public Task<IReadOnlyList<DeliveryZoneDto>> Zones(CancellationToken ct) => _admin.ListZonesAsync(ct);

    [HttpPost("api/v{version:apiVersion}/admin/delivery-zones")]
    public Task<DeliveryZoneDto> CreateZone([FromBody] UpsertDeliveryZoneInput i, CancellationToken ct) => _admin.UpsertZoneAsync(null, i, ct);

    [HttpPut("api/v{version:apiVersion}/admin/delivery-zones/{id:guid}")]
    public Task<DeliveryZoneDto> UpdateZone(Guid id, [FromBody] UpsertDeliveryZoneInput i, CancellationToken ct) => _admin.UpsertZoneAsync(id, i, ct);

    [HttpDelete("api/v{version:apiVersion}/admin/delivery-zones/{id:guid}")]
    public async Task<IActionResult> DeleteZone(Guid id, CancellationToken ct) { await _admin.DeleteZoneAsync(id, ct); return NoContent(); }

    [HttpGet("api/v{version:apiVersion}/admin/peak-hour-rules")]
    public Task<IReadOnlyList<PeakHourRuleDto>> Rules(CancellationToken ct) => _admin.ListRulesAsync(ct);

    [HttpPost("api/v{version:apiVersion}/admin/peak-hour-rules")]
    public Task<PeakHourRuleDto> CreateRule([FromBody] UpsertPeakHourRuleInput i, CancellationToken ct) => _admin.UpsertRuleAsync(null, i, ct);

    [HttpPut("api/v{version:apiVersion}/admin/peak-hour-rules/{id:guid}")]
    public Task<PeakHourRuleDto> UpdateRule(Guid id, [FromBody] UpsertPeakHourRuleInput i, CancellationToken ct) => _admin.UpsertRuleAsync(id, i, ct);

    [HttpDelete("api/v{version:apiVersion}/admin/peak-hour-rules/{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct) { await _admin.DeleteRuleAsync(id, ct); return NoContent(); }
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class FulfillmentSlaController : ControllerBase
{
    private readonly ISlaService _sla;
    public FulfillmentSlaController(ISlaService sla) => _sla = sla;

    [HttpGet("api/v{version:apiVersion}/admin/fulfillment-slas")]
    public Task<IReadOnlyList<FulfillmentSlaDto>> List(CancellationToken ct) => _sla.ListConfigAsync(ct);

    [HttpPut("api/v{version:apiVersion}/admin/fulfillment-slas")]
    public Task<FulfillmentSlaDto> Upsert([FromBody] UpsertFulfillmentSlaInput i, CancellationToken ct) => _sla.UpsertConfigAsync(null, i, ct);

    [HttpDelete("api/v{version:apiVersion}/admin/fulfillment-slas/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _sla.DeleteConfigAsync(id, ct); return NoContent(); }

    [HttpGet("api/v{version:apiVersion}/admin/sla-breaches")]
    public Task<PageResult<SlaBreachDto>> Breaches([FromQuery] Guid? storeId, [FromQuery] bool openOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _sla.ListBreachesAsync(storeId, openOnly, page, pageSize, ct);

    [HttpGet("api/v{version:apiVersion}/stores/me/sla-breaches")]
    public Task<PageResult<SlaBreachDto>> MyBreaches([FromQuery] bool openOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => _sla.ListMyBreachesAsync(openOnly, page, pageSize, ct);

    [HttpGet("api/v{version:apiVersion}/orders/{orderId:guid}/sla")]
    public Task<IReadOnlyList<OrderSlaDto>> OrderSla(Guid orderId, CancellationToken ct) => _sla.GetOrderSlaAsync(orderId, ct);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class ReturnPickupsController : ControllerBase
{
    private readonly IReturnPickupService _svc;
    public ReturnPickupsController(IReturnPickupService svc) => _svc = svc;

    [HttpGet("api/v{version:apiVersion}/returns/{returnId:guid}/pickup")]
    public Task<ReturnPickupDto?> Get(Guid returnId, CancellationToken ct) => _svc.GetByReturnAsync(returnId, ct);

    [HttpPost("api/v{version:apiVersion}/returns/{returnId:guid}/pickup")]
    public Task<ReturnPickupDto> Request(Guid returnId, [FromBody] RequestReturnPickupInput i, CancellationToken ct) => _svc.RequestAsync(returnId, i, ct);

    [HttpPut("api/v{version:apiVersion}/return-pickups/{id:guid}/reschedule")]
    public Task<ReturnPickupDto> Reschedule(Guid id, [FromBody] ReschedulePickupInput i, CancellationToken ct) => _svc.RescheduleAsync(id, i, ct);

    [HttpGet("api/v{version:apiVersion}/return-pickups/admin")]
    public Task<PageResult<ReturnPickupDto>> Admin([FromQuery] ReturnPickupStatus? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default) => _svc.ListAdminAsync(status, page, pageSize, ct);

    [HttpPost("api/v{version:apiVersion}/return-pickups/{id:guid}/assign")]
    public Task<ReturnPickupDto> Assign(Guid id, [FromBody] AssignPickupInput i, CancellationToken ct) => _svc.AssignAsync(id, i, ct);

    [HttpGet("api/v{version:apiVersion}/return-pickups/mine")]
    public Task<IReadOnlyList<ReturnPickupDto>> Mine([FromQuery] bool includeClosed = false, CancellationToken ct = default)
        => _svc.ListMineAsync(includeClosed, ct);

    [HttpPut("api/v{version:apiVersion}/return-pickups/{id:guid}/status")]
    public Task<ReturnPickupDto> CourierUpdate(Guid id, [FromBody] CourierPickupUpdateInput i, CancellationToken ct) => _svc.CourierUpdateAsync(id, i, ct);

    [HttpPost("api/v{version:apiVersion}/return-pickups/{id:guid}/confirm-receipt")]
    public Task<ReturnPickupDto> Confirm(Guid id, [FromBody] ConfirmReceiptInput? i, CancellationToken ct) => _svc.ConfirmReceiptAsync(id, i?.Note, ct);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class PartialRefundsController : ControllerBase
{
    private readonly IPartialRefundService _svc;
    private readonly IStepUpAuthService _stepUp;
    public PartialRefundsController(IPartialRefundService svc, IStepUpAuthService stepUp) { _svc = svc; _stepUp = stepUp; }

    [HttpPost("api/v{version:apiVersion}/orders/{orderId:guid}/partial-refund")]
    [Pawzaroo.Api.Authorization.Permission(Pawzaroo.Application.Common.Permissions.Permissions.Orders.Refund)]
    public async Task<PartialRefundResultDto> Refund(Guid orderId, [FromBody] PartialRefundInput input, CancellationToken ct)
    {
        await _stepUp.RequireRecentAsync(ct);
        return await _svc.RefundItemsAsync(orderId, input.Items, input.Reason, ct);
    }

    [HttpGet("api/v{version:apiVersion}/orders/{orderId:guid}/refunds")]
    public Task<OrderRefundSummaryDto> Summary(Guid orderId, CancellationToken ct) => _svc.GetSummaryAsync(orderId, ct);

    [HttpGet("api/v{version:apiVersion}/orders/{orderId:guid}/store-split")]
    public Task<IReadOnlyList<StoreOrderSplitDto>> Split(Guid orderId, CancellationToken ct) => _svc.GetStoreSplitAsync(orderId, ct);
}

[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class StoreMetricsController : ControllerBase
{
    private readonly IStoreMetricsService _svc;
    public StoreMetricsController(IStoreMetricsService svc) => _svc = svc;

    [HttpGet("api/v{version:apiVersion}/stores/me/metrics")]
    public Task<StoreMetricsDto> Mine([FromQuery] int days = 30, CancellationToken ct = default) => _svc.GetMineAsync(days, ct);
}
