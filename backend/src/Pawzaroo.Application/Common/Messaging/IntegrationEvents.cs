namespace Pawzaroo.Application.Common.Messaging;

/// <summary>
/// Common envelope. Every Kafka message we publish wraps the payload so we can
/// version contracts, route by type, and dedupe by id without parsing the body.
/// </summary>
public record EventEnvelope<T>(
    Guid EventId,
    string EventType,
    string Version,
    DateTime OccurredAt,
    string? CorrelationId,
    string? UserId,
    T Data);

/// <summary>Marker interface so consumers can be generic over events.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

// =============================================================================
//  Identity / user
// =============================================================================
public record UserRegistered(Guid EventId, Guid UserId, string Email, string DisplayName, DateTime OccurredAt) : IIntegrationEvent;
public record UserLoggedIn(Guid EventId, Guid UserId, string Ip, DateTime OccurredAt) : IIntegrationEvent;
public record UserEmailVerified(Guid EventId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;
public record UserDeactivated(Guid EventId, Guid UserId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;

public record RoleAssigned(Guid EventId, Guid UserId, string RoleName, Guid AssignedBy, DateTime OccurredAt) : IIntegrationEvent;
public record PermissionsChanged(Guid EventId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Pets / Social / Feed
// =============================================================================
public record FeedPostCreated(Guid EventId, Guid PostId, Guid AuthorId, string? Caption, IReadOnlyList<string> MediaUrls, DateTime OccurredAt) : IIntegrationEvent;
public record FeedPostReacted(Guid EventId, Guid PostId, Guid UserId, string Reaction, DateTime OccurredAt) : IIntegrationEvent;
public record FeedPostShared(Guid EventId, Guid PostId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;
public record CommentAdded(Guid EventId, Guid CommentId, Guid PostId, Guid AuthorId, string Body, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Adoption
// =============================================================================
public record AdoptionListingCreatedIntegration(Guid EventId, Guid ListingId, Guid OwnerId, DateTime OccurredAt) : IIntegrationEvent;
public record AdoptionListingApprovedIntegration(Guid EventId, Guid ListingId, Guid ApprovedBy, DateTime OccurredAt) : IIntegrationEvent;
public record AdoptionListingRejectedIntegration(Guid EventId, Guid ListingId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Messaging
// =============================================================================
public record MessageSent(Guid EventId, Guid MessageId, Guid ConversationId, Guid SenderId, IReadOnlyList<Guid> Recipients, string Preview, DateTime OccurredAt) : IIntegrationEvent;
public record MessageRead(Guid EventId, Guid MessageId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Vet / Appointment
// =============================================================================
public record DoctorRegistered(Guid EventId, Guid DoctorId, Guid UserId, DateTime OccurredAt) : IIntegrationEvent;
public record DoctorApproved(Guid EventId, Guid DoctorId, Guid ApprovedBy, DateTime OccurredAt) : IIntegrationEvent;
public record DoctorRejected(Guid EventId, Guid DoctorId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;

public record AppointmentBooked(Guid EventId, Guid AppointmentId, Guid DoctorId, Guid UserId, DateTime StartUtc, DateTime OccurredAt) : IIntegrationEvent;
public record AppointmentCancelled(Guid EventId, Guid AppointmentId, Guid CancelledBy, string? Reason, DateTime OccurredAt) : IIntegrationEvent;
public record AppointmentRescheduled(Guid EventId, Guid AppointmentId, DateTime NewStartUtc, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Marketplace
// =============================================================================
public record StoreRegisteredIntegration(Guid EventId, Guid StoreId, Guid OwnerUserId, DateTime OccurredAt) : IIntegrationEvent;
public record StoreApprovedIntegration(Guid EventId, Guid StoreId, Guid ApprovedBy, DateTime OccurredAt) : IIntegrationEvent;
public record StoreSuspendedIntegration(Guid EventId, Guid StoreId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;

public record ProductCreatedIntegration(Guid EventId, Guid ProductId, Guid StoreId, DateTime OccurredAt) : IIntegrationEvent;
public record ProductPriceChanged(Guid EventId, Guid ProductId, decimal OldPrice, decimal NewPrice, DateTime OccurredAt) : IIntegrationEvent;
public record ProductStockLow(Guid EventId, Guid ProductId, Guid StoreId, int RemainingStock, DateTime OccurredAt) : IIntegrationEvent;

public record OrderPlacedIntegration(
    Guid EventId, Guid OrderId, string OrderNumber, Guid UserId,
    decimal Total, int ItemCount, IReadOnlyList<Guid> StoreIds, DateTime OccurredAt) : IIntegrationEvent;
public record OrderCancelledIntegration(Guid EventId, Guid OrderId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;
public record OrderShipped(Guid EventId, Guid OrderId, string? TrackingNumber, DateTime OccurredAt) : IIntegrationEvent;

public record PaymentCompleted(Guid EventId, Guid OrderId, Guid PaymentId, decimal Amount, string Method, DateTime OccurredAt) : IIntegrationEvent;
public record PaymentFailedIntegration(Guid EventId, Guid OrderId, string? Reason, DateTime OccurredAt) : IIntegrationEvent;

public record ReviewSubmitted(Guid EventId, Guid ReviewId, Guid ProductId, Guid UserId, int Rating, DateTime OccurredAt) : IIntegrationEvent;

// =============================================================================
//  Cross-cutting
// =============================================================================
public record NotificationCreated(Guid EventId, Guid NotificationId, Guid UserId, string Title, string Body, object? Payload, DateTime OccurredAt) : IIntegrationEvent;
public record AuditLogCreated(Guid EventId, string Action, string EntityType, Guid? EntityId, Guid? ActorId, string? Detail, DateTime OccurredAt) : IIntegrationEvent;
