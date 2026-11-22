using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>A delivery zone: a set of cities with a base fee, free-shipping threshold and ETA range. StoreId null = platform-wide.</summary>
public class DeliveryZone : AuditableEntity
{
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = default!;
    /// <summary>Comma separated city names (case-insensitive match).</summary>
    public string Cities { get; set; } = "";
    public decimal BaseFee { get; set; }
    /// <summary>Subtotal at or above which shipping is free; null/0 = never free.</summary>
    public decimal? FreeShippingThreshold { get; set; }
    public int EstimatedMinHours { get; set; } = 24;
    public int EstimatedMaxHours { get; set; } = 72;
    public bool IsActive { get; set; } = true;
}

/// <summary>Surcharge applied inside a weekly time window (local time, may wrap midnight). ZoneId null = all zones.</summary>
public class PeakHourRule : AuditableEntity
{
    public Guid? ZoneId { get; set; }
    /// <summary>Bitmask of System.DayOfWeek values (bit 0 = Sunday ... bit 6 = Saturday). 0 = no day.</summary>
    public int DaysOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal SurchargeFixed { get; set; }
    public decimal SurchargePercent { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Label { get; set; }
}

/// <summary>Fulfillment SLA targets. StoreId null = platform default.</summary>
public class FulfillmentSla : AuditableEntity
{
    public Guid? StoreId { get; set; }
    public int MaxHoursToShip { get; set; } = 48;
    public int MaxHoursToDeliver { get; set; } = 168;
    public string? PenaltyNote { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum SlaBreachKind { Ship = 0, Deliver = 1 }

/// <summary>One row per (order, store, kind); written idempotently by the SLA monitor.</summary>
public class SlaBreach : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid StoreId { get; set; }
    public SlaBreachKind Kind { get; set; }
    public DateTime BreachedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public enum ReturnPickupStatus { Requested = 0, Scheduled = 1, PickedUp = 2, InTransit = 3, ReceivedByStore = 4, Failed = 5 }

public class ReturnPickup : AuditableEntity
{
    public Guid ReturnRequestId { get; set; }
    public DateTime ScheduledWindowStart { get; set; }
    public DateTime ScheduledWindowEnd { get; set; }
    public string AddressSnapshot { get; set; } = default!;
    public string? Notes { get; set; }
    public ReturnPickupStatus Status { get; set; } = ReturnPickupStatus.Requested;
    public Guid? DeliveryUserId { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ProofPhotoUrl { get; set; }
    public string? ProofSignatureName { get; set; }
    public int RescheduleCount { get; set; }
}

public class ReturnPickupCheckpoint : BaseEntity
{
    public Guid ReturnPickupId { get; set; }
    public ReturnPickupStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Partial (per item) refund record. Refunded amount of an order is computed from these rows.</summary>
public class OrderRefund : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = default!;
    /// <summary>"RefundInitiated" until a gateway refund API confirms; no external call is faked.</summary>
    public string GatewayStatus { get; set; } = "RefundInitiated";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ByUserId { get; set; }
}
