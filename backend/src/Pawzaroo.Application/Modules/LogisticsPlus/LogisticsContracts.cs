using Pawzaroo.Domain.Store;
using Pawzaroo.Application.Modules.Marketplace.Dtos;

namespace Pawzaroo.Application.Modules.LogisticsPlus;

// ------------------------------------------------------------------ shipping quote

public record ShippingQuoteDto(
    bool ZoneMatched, Guid? ZoneId, string? ZoneName, string Currency,
    decimal BaseFee, decimal PeakSurcharge, string? PeakLabel, bool FreeShippingApplied, decimal Total,
    int? EtaMinHours, int? EtaMaxHours, DateTime? EtaEarliest, DateTime? EtaLatest);

public interface IShippingQuoteService
{
    /// <summary>Fee breakdown + ETA range for one store and destination. ZoneMatched=false when no active zone covers the city.</summary>
    Task<ShippingQuoteDto> QuoteAsync(Guid? storeId, string? destinationCity, decimal subtotal, DateTime at, CancellationToken ct = default);

    /// <summary>
    /// Order-level shipping fee for checkout: quotes the net subtotal of each store and sums the fees.
    /// Returns null (caller keeps the legacy flat-fee logic) unless EVERY store resolves to a zone.
    /// </summary>
    Task<decimal?> TryQuoteOrderFeeAsync(IReadOnlyList<(Guid StoreId, decimal NetSubtotal)> stores, string? destinationCity, DateTime at, CancellationToken ct = default);
}

public record DeliveryZoneDto(
    Guid Id, Guid? StoreId, string Name, IReadOnlyList<string> Cities, decimal BaseFee, decimal? FreeShippingThreshold,
    int EstimatedMinHours, int EstimatedMaxHours, bool IsActive);

public record UpsertDeliveryZoneInput(
    Guid? StoreId, string Name, IReadOnlyList<string> Cities, decimal BaseFee, decimal? FreeShippingThreshold,
    int EstimatedMinHours, int EstimatedMaxHours, bool IsActive);

public record PeakHourRuleDto(
    Guid Id, Guid? ZoneId, IReadOnlyList<int> Days, string StartTime, string EndTime,
    decimal SurchargeFixed, decimal SurchargePercent, bool IsActive, string? Label);

public record UpsertPeakHourRuleInput(
    Guid? ZoneId, IReadOnlyList<int> Days, string StartTime, string EndTime,
    decimal SurchargeFixed, decimal SurchargePercent, bool IsActive, string? Label);

public interface IDeliveryZoneAdminService
{
    Task<IReadOnlyList<DeliveryZoneDto>> ListZonesAsync(CancellationToken ct = default);
    Task<DeliveryZoneDto> UpsertZoneAsync(Guid? id, UpsertDeliveryZoneInput input, CancellationToken ct = default);
    Task DeleteZoneAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PeakHourRuleDto>> ListRulesAsync(CancellationToken ct = default);
    Task<PeakHourRuleDto> UpsertRuleAsync(Guid? id, UpsertPeakHourRuleInput input, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
}

// ------------------------------------------------------------------ SLA

public enum SlaStatus { OnTrack = 0, AtRisk = 1, Breached = 2 }

public record FulfillmentSlaDto(Guid? Id, Guid? StoreId, int MaxHoursToShip, int MaxHoursToDeliver, string? PenaltyNote, bool IsActive);
public record UpsertFulfillmentSlaInput(Guid? StoreId, int MaxHoursToShip, int MaxHoursToDeliver, string? PenaltyNote, bool IsActive);

public record OrderSlaDto(
    Guid OrderId, Guid StoreId, SlaStatus ShipStatus, SlaStatus DeliverStatus, SlaStatus Overall,
    DateTime StartedAt, DateTime ShipDueAt, DateTime DeliverDueAt, DateTime? ShippedAt, DateTime? DeliveredAt);

public record SlaBreachDto(
    Guid Id, Guid OrderId, string? OrderNumber, Guid StoreId, string? StoreName, SlaBreachKind Kind, DateTime BreachedAt, DateTime? ResolvedAt);

public interface ISlaService
{
    Task<IReadOnlyList<FulfillmentSlaDto>> ListConfigAsync(CancellationToken ct = default);
    Task<FulfillmentSlaDto> UpsertConfigAsync(Guid? id, UpsertFulfillmentSlaInput input, CancellationToken ct = default);
    Task DeleteConfigAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<OrderSlaDto>> GetOrderSlaAsync(Guid orderId, CancellationToken ct = default);
    Task<PageResult<SlaBreachDto>> ListBreachesAsync(Guid? storeId, bool openOnly, int page, int pageSize, CancellationToken ct = default);
    Task<PageResult<SlaBreachDto>> ListMyBreachesAsync(bool openOnly, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>System-level monitor (no permission checks): flags late shipments and deliveries, notifies, resolves.</summary>
public interface ISlaMonitor
{
    /// <summary>Returns the number of NEW breaches recorded.</summary>
    Task<int> RunAsync(CancellationToken ct = default);
}

// ------------------------------------------------------------------ return pickups

public record ReturnPickupCheckpointDto(Guid Id, ReturnPickupStatus Status, string? Note, DateTime OccurredAt);

public record ReturnPickupDto(
    Guid Id, Guid ReturnRequestId, Guid OrderId, string? OrderNumber, string? ProductName, ReturnPickupStatus Status,
    DateTime WindowStart, DateTime WindowEnd, string Address, string? Notes,
    Guid? DeliveryUserId, string? DeliveryUserName, DateTime? PickedUpAt, DateTime? ReceivedAt,
    string? ProofPhotoUrl, string? ProofSignatureName, int RescheduleCount, DateTime CreatedAt,
    IReadOnlyList<ReturnPickupCheckpointDto> Checkpoints);

public record RequestReturnPickupInput(DateTime WindowStart, DateTime WindowEnd, string? Address, string? Notes);
public record ReschedulePickupInput(DateTime WindowStart, DateTime WindowEnd, string? Notes);
public record AssignPickupInput(Guid DeliveryUserId);
public record CourierPickupUpdateInput(ReturnPickupStatus Status, string? Note, string? ProofPhotoUrl, string? ProofSignatureName);
public record ConfirmReceiptInput(string? Note);

public interface IReturnPickupService
{
    Task<ReturnPickupDto> RequestAsync(Guid returnRequestId, RequestReturnPickupInput input, CancellationToken ct = default);
    Task<ReturnPickupDto> RescheduleAsync(Guid pickupId, ReschedulePickupInput input, CancellationToken ct = default);
    Task<ReturnPickupDto?> GetByReturnAsync(Guid returnRequestId, CancellationToken ct = default);
    Task<PageResult<ReturnPickupDto>> ListAdminAsync(ReturnPickupStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<ReturnPickupDto> AssignAsync(Guid pickupId, AssignPickupInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnPickupDto>> ListMineAsync(bool includeClosed, CancellationToken ct = default);
    Task<ReturnPickupDto> CourierUpdateAsync(Guid pickupId, CourierPickupUpdateInput input, CancellationToken ct = default);
    Task<ReturnPickupDto> ConfirmReceiptAsync(Guid pickupId, string? note, CancellationToken ct = default);
}

// ------------------------------------------------------------------ split view + partial refunds

public record StoreOrderSplitDto(
    Guid StoreId, string? StoreName, decimal GrossSubtotal, decimal Discount, decimal NetSubtotal,
    decimal Shipping, decimal Tax, decimal Commission, decimal Total, decimal Refunded, decimal Payable);

public record PartialRefundItemInput(Guid OrderItemId, int Quantity);
public record PartialRefundInput(IReadOnlyList<PartialRefundItemInput> Items, string Reason);

public record PartialRefundLineDto(Guid OrderItemId, int Quantity, decimal Amount);
public record PartialRefundResultDto(Guid OrderId, decimal Amount, string GatewayStatus, IReadOnlyList<PartialRefundLineDto> Lines, decimal OrderRefundedTotal);

public record OrderRefundDto(Guid Id, Guid OrderItemId, int Quantity, decimal Amount, string Reason, string GatewayStatus, DateTime CreatedAt, Guid? ByUserId);
public record OrderRefundSummaryDto(Guid OrderId, decimal OrderTotal, decimal RefundedAmount, IReadOnlyList<OrderRefundDto> Refunds);

public interface IPartialRefundService
{
    Task<PartialRefundResultDto> RefundItemsAsync(Guid orderId, IReadOnlyList<PartialRefundItemInput> items, string reason, CancellationToken ct = default);
    Task<OrderRefundSummaryDto> GetSummaryAsync(Guid orderId, CancellationToken ct = default);
    Task<IReadOnlyList<StoreOrderSplitDto>> GetStoreSplitAsync(Guid orderId, CancellationToken ct = default);
}

// ------------------------------------------------------------------ store metrics

public record MetricPointDto(string Label, decimal Value);
public record TopProductMetricDto(Guid ProductId, string Name, int Units, decimal Revenue);

public record StoreMetricsDto(
    Guid StoreId, int Days, int Orders, int CancelledOrders, decimal CancellationRate,
    int ShipSlaOrders, int LateShipments, decimal LateShipmentRate,
    int ReturnRequests, decimal ReturnRate,
    int RejectedReturns, decimal DisputeRate,
    int ProductViewers, decimal ConversionRate, bool ConversionAvailable,
    decimal AverageRating, int ReviewCount, decimal Revenue,
    IReadOnlyList<MetricPointDto> DailyRevenue, IReadOnlyList<TopProductMetricDto> TopProducts);

public interface IStoreMetricsService
{
    Task<StoreMetricsDto> GetMineAsync(int days, CancellationToken ct = default);
}
