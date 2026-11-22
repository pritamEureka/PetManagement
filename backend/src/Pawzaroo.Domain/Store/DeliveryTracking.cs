using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

/// <summary>Courier-agnostic tracking status. Mapped onto ShipmentStatus / DeliveryAssignmentStatus by the tracking service.</summary>
public enum TrackingStatus
{
    LabelCreated = 0,
    PickedUp = 1,
    InTransit = 2,
    OutForDelivery = 3,
    Delivered = 4,
    DeliveryFailed = 5,
    Returned = 6,
    Exception = 7
}

public enum CheckpointSource { Courier = 0, Webhook = 1, Manual = 2 }

/// <summary>Append-only tracking event for an order.</summary>
public class DeliveryCheckpoint : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? DeliveryAssignmentId { get; set; }
    public TrackingStatus Status { get; set; }
    public string? Description { get; set; }
    public string? LocationText { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public CheckpointSource Source { get; set; } = CheckpointSource.Manual;
    /// <summary>Courier provider name for webhook/courier events.</summary>
    public string? Provider { get; set; }
    /// <summary>Provider-supplied event id, used to de-duplicate webhook deliveries.</summary>
    public string? ExternalEventId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}

/// <summary>Proof of delivery: at most one per order.</summary>
public class DeliveryProof : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? DeliveryAssignmentId { get; set; }
    public string? PhotoUrl { get; set; }
    public string? SignatureName { get; set; }
    public string? OtpCode { get; set; }
    public string? RecipientName { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public Guid? CapturedByUserId { get; set; }
}
