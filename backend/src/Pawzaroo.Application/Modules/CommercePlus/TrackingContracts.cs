using Pawzaroo.Domain.Store;

namespace Pawzaroo.Application.Modules.CommercePlus;

public record DeliveryCheckpointDto(
    Guid Id, TrackingStatus Status, string? Description, string? LocationText,
    double? Latitude, double? Longitude, CheckpointSource Source, string? Provider, DateTime OccurredAt);

public record DeliveryProofDto(
    Guid Id, string? PhotoUrl, string? SignatureName, bool OtpProvided, string? RecipientName, DateTime CapturedAt);

public record OrderTrackingDto(
    Guid OrderId, string OrderNumber, string ShipmentStatus, string? TrackingNumber, string? Courier,
    IReadOnlyList<DeliveryCheckpointDto> Checkpoints, DeliveryProofDto? Proof);

public record AddCheckpointInput(
    TrackingStatus Status, string? Description, string? LocationText,
    double? Latitude, double? Longitude, DateTime? OccurredAt);

public record AddDeliveryProofInput(
    string? PhotoUrl, string? SignatureName, string? OtpCode, string? RecipientName);

/// <summary>Normalized event produced by webhook ingestion.</summary>
public record ExternalTrackingEvent(
    string Provider, string? ExternalEventId, string? TrackingNumber, TrackingStatus Status,
    string? Description, string? LocationText, double? Latitude, double? Longitude, DateTime OccurredAt);

public interface IDeliveryTrackingService
{
    Task<OrderTrackingDto> GetTimelineAsync(Guid orderId, CancellationToken ct = default);
    Task<Guid> AddCheckpointAsync(Guid orderId, AddCheckpointInput input, CancellationToken ct = default);
    Task AddProofAsync(Guid orderId, AddDeliveryProofInput input, CancellationToken ct = default);
    /// <summary>Webhook ingestion (no user context). Returns false when the event was a duplicate or matched no order.</summary>
    Task<bool> IngestExternalEventAsync(ExternalTrackingEvent evt, CancellationToken ct = default);
}

/// <summary>System-level status application, implemented by DeliveryService (no user permission checks).</summary>
public interface IDeliveryStatusApplier
{
    Task ApplyTrackingStatusAsync(Guid orderId, TrackingStatus status, string? note, CancellationToken ct = default);
}
