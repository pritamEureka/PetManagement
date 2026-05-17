namespace Pawzaroo.Application.Common.Messaging;

/// <summary>
/// All Kafka topic names. Match the naming scheme used by the existing module
/// events (<c>pawzaroo.&lt;domain&gt;.events</c>) so consumers can wildcard
/// across a domain.
/// </summary>
public static class KafkaTopics
{
    // Identity / auth
    public const string UserEvents          = "pawzaroo.user.events";          // UserRegistered, UserLoggedIn, ...
    public const string RbacEvents          = "pawzaroo.rbac.events";          // RoleAssigned, PermissionsChanged

    // Pets / social
    public const string PetEvents           = "pawzaroo.pet.events";
    public const string FeedPostEvents      = "pawzaroo.feed.post.events";     // FeedPostCreated, ReactionAdded, CommentAdded
    public const string FeedCommentEvents   = "pawzaroo.feed.comment.events";

    // Adoption
    public const string AdoptionEvents      = "pawzaroo.adoption.events";
    public const string AdoptionApprovals   = "pawzaroo.adoption.approvals";

    // Messaging
    public const string MessageEvents       = "pawzaroo.message.events";       // MessageSent, MessageRead

    // Vet
    public const string VetEvents           = "pawzaroo.vet.events";           // DoctorRegistered, DoctorApproved
    public const string AppointmentEvents   = "pawzaroo.appointment.events";   // AppointmentBooked, Cancelled

    // Marketplace
    public const string StoreEvents         = "pawzaroo.marketplace.store";
    public const string ProductEvents       = "pawzaroo.marketplace.product";
    public const string InventoryEvents     = "pawzaroo.marketplace.inventory";
    public const string OrderEvents         = "pawzaroo.marketplace.order";
    public const string PaymentEvents       = "pawzaroo.marketplace.payment";
    public const string ReviewEvents        = "pawzaroo.marketplace.review";
    public const string MarketplaceAdmin    = "pawzaroo.marketplace.admin";

    // Cross-cutting
    public const string Notifications       = "pawzaroo.notifications";        // NotificationCreated
    public const string Audit               = "pawzaroo.audit";                // AuditLogCreated

    // Dead-letter — one DLQ per consumer group, suffix appended at runtime
    public const string DeadLetterSuffix    = ".dlq";

    /// <summary>Convenience: enumerate all topics for auto-creation / admin tooling.</summary>
    public static IReadOnlyList<string> All() => new[]
    {
        UserEvents, RbacEvents,
        PetEvents, FeedPostEvents, FeedCommentEvents,
        AdoptionEvents, AdoptionApprovals,
        MessageEvents,
        VetEvents, AppointmentEvents,
        StoreEvents, ProductEvents, InventoryEvents,
        OrderEvents, PaymentEvents, ReviewEvents, MarketplaceAdmin,
        Notifications, Audit
    };
}

/// <summary>Consumer group naming. Each downstream concern uses its own group.</summary>
public static class KafkaConsumerGroups
{
    public const string ApiInbox             = "pawzaroo-api-inbox";
    public const string NotificationDispatch = "pawzaroo-worker-notifications";
    public const string OrderProjector       = "pawzaroo-worker-order-projector";
    public const string AuditWriter          = "pawzaroo-worker-audit";
    public const string SearchIndexer        = "pawzaroo-worker-search";
    public const string FeedDenormalizer     = "pawzaroo-worker-feed-denorm";
}
