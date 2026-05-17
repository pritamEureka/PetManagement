namespace Pawzaroo.Application.Modules.Marketplace.Events;

/// <summary>
/// Kafka topic names. Producers fan out events here; downstream services
/// (notification, search-indexer, analytics) subscribe.
/// </summary>
public static class MarketplaceTopics
{
    public const string StoreEvents     = "pawzaroo.marketplace.store";
    public const string ProductEvents   = "pawzaroo.marketplace.product";
    public const string InventoryEvents = "pawzaroo.marketplace.inventory";
    public const string OrderEvents     = "pawzaroo.marketplace.order";
    public const string PaymentEvents   = "pawzaroo.marketplace.payment";
    public const string ReviewEvents    = "pawzaroo.marketplace.review";
    public const string AdminEvents     = "pawzaroo.marketplace.admin";
}

// ----- Store / owner ----------------------------------------------------------
public record StoreOwnerKycSubmitted(Guid ProfileId, Guid UserId, DateTime At);
public record StoreOwnerKycApproved(Guid ProfileId, Guid UserId, Guid ApprovedBy, DateTime At);
public record StoreOwnerKycRejected(Guid ProfileId, Guid UserId, Guid RejectedBy, string? Reason, DateTime At);

public record StoreRegistered(Guid StoreId, Guid OwnerUserId, DateTime At);
public record StoreApproved(Guid StoreId, Guid ApprovedBy, DateTime At);
public record StoreRejected(Guid StoreId, Guid RejectedBy, string? Reason, DateTime At);
public record StoreSuspended(Guid StoreId, Guid SuspendedBy, string? Reason, DateTime At);
public record StoreFeatured(Guid StoreId, bool Featured, DateTime At);
public record StoreUpdated(Guid StoreId, DateTime At);

// ----- Product ---------------------------------------------------------------
public record ProductCreated(Guid ProductId, Guid StoreId, DateTime At);
public record ProductUpdated(Guid ProductId, Guid StoreId, DateTime At);
public record ProductDeleted(Guid ProductId, Guid StoreId, DateTime At);
public record ProductFeatured(Guid ProductId, bool Featured, DateTime At);
public record ProductPublished(Guid ProductId, bool Active, DateTime At);

// ----- Inventory --------------------------------------------------------------
public record InventoryAdjusted(
    Guid ProductId, Guid StoreId, int QuantityChange, int QuantityAfter,
    string Reason, Guid? OrderId, DateTime At);

public record LowStockWarning(Guid ProductId, Guid StoreId, int RemainingStock, DateTime At);

// ----- Cart -------------------------------------------------------------------
public record CartItemAdded(Guid UserId, Guid CartId, Guid ProductId, int Quantity, DateTime At);
public record CartItemRemoved(Guid UserId, Guid CartId, Guid ProductId, DateTime At);
public record CartCleared(Guid UserId, Guid CartId, DateTime At);

// ----- Order ------------------------------------------------------------------
public record OrderPlaced(
    Guid OrderId, string OrderNumber, Guid UserId,
    decimal Total, int ItemCount, IReadOnlyList<Guid> StoreIds, DateTime At);

public record OrderStatusChanged(Guid OrderId, string OrderNumber, string Status, DateTime At);
public record OrderShipmentStatusChanged(Guid OrderId, string OrderNumber, string ShipmentStatus, string? TrackingNumber, DateTime At);
public record OrderCancelled(Guid OrderId, string OrderNumber, Guid CancelledBy, string? Reason, DateTime At);
public record OrderRefunded(Guid OrderId, string OrderNumber, decimal Amount, DateTime At);

// ----- Payment ---------------------------------------------------------------
public record PaymentInitiated(Guid OrderId, Guid PaymentId, decimal Amount, string Method, DateTime At);
public record PaymentSucceeded(Guid OrderId, Guid PaymentId, decimal Amount, string? TransactionRef, DateTime At);
public record PaymentFailed(Guid OrderId, Guid PaymentId, string? Reason, DateTime At);

// ----- Review -----------------------------------------------------------------
public record ProductReviewCreated(Guid ReviewId, Guid ProductId, Guid UserId, int Rating, DateTime At);
public record StoreReviewCreated(Guid ReviewId, Guid StoreId, Guid UserId, int Rating, DateTime At);

// ----- Return / refund --------------------------------------------------------
public record ReturnRequested(Guid RequestId, Guid OrderItemId, Guid UserId, DateTime At);
public record ReturnDecided(Guid RequestId, Guid OrderItemId, string Decision, decimal? RefundAmount, DateTime At);

// ----- Commission -------------------------------------------------------------
public record CommissionConfigurationChanged(Guid ConfigId, string Scope, Guid? StoreId, Guid? CategoryId, decimal Percent, DateTime At);
