using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// Links an Order to a User with the DeliveryUser role. An order has at most
/// one active assignment at a time (enforced by a unique index on OrderId).
/// State transitions:
///   Assigned → PickedUp → InTransit → OutForDelivery → Delivered
/// Failed is a terminal state usable from any non-Delivered state.
///
/// When the assignment transitions, DeliveryService also bumps the parent
/// Order's ShipmentStatus so admin/buyer views stay in sync without joining.
/// </summary>
public class DeliveryAssignment : AuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid DeliveryUserId { get; set; }
    public User DeliveryUser { get; set; } = default!;

    public DeliveryAssignmentStatus Status { get; set; } = DeliveryAssignmentStatus.Assigned;
    public string? Notes { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public Guid AssignedByUserId { get; set; }
}
