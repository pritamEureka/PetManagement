namespace Pawzaroo.Infrastructure.Messaging;

/// <summary>
/// Kafka client configuration, bound from <c>Kafka:*</c> in appsettings.
/// Topic names default to the catalog in <see cref="Pawzaroo.Application.Common.Messaging.KafkaTopics"/>;
/// the duplicates here exist only so deployments can override individual
/// names from configuration without code changes.
/// </summary>
public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ConsumerGroupId  { get; set; } = "pawzaroo-api";

    /// <summary>Optional SASL/SSL auth — leave null for plaintext local dev.</summary>
    public string? SaslMechanism   { get; set; }
    public string? SaslUsername    { get; set; }
    public string? SaslPassword    { get; set; }
    public string? SecurityProtocol { get; set; } // "Plaintext", "Ssl", "SaslPlaintext", "SaslSsl"

    /// <summary>How many times the consumer retries a message before parking it in the DLQ.</summary>
    public int RetryAttempts       { get; set; } = 3;
    public int RetryBaseDelayMs    { get; set; } = 500;

    /// <summary>
    /// Run the embedded API consumer (off by default in the API; the worker
    /// always runs its consumers). Useful for dev where you don't want to boot
    /// the worker.
    /// </summary>
    public bool EnableInProcessConsumer { get; set; } = true;

    public KafkaTopics Topics { get; set; } = new();
}

/// <summary>
/// Overridable topic names. Defaults match <see cref="Pawzaroo.Application.Common.Messaging.KafkaTopics"/>.
/// Keep these in sync if you rename one — the centralized constant catalog is
/// the source of truth.
/// </summary>
public class KafkaTopics
{
    public string UserEvents          { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.UserEvents;
    public string RbacEvents          { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.RbacEvents;
    public string PostEvents          { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.FeedPostEvents;
    public string CommentEvents       { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.FeedCommentEvents;
    public string AdoptionEvents      { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.AdoptionEvents;
    public string MessageEvents       { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.MessageEvents;
    public string VetEvents           { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.VetEvents;
    public string AppointmentEvents   { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.AppointmentEvents;
    public string StoreEvents         { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.StoreEvents;
    public string ProductEvents       { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.ProductEvents;
    public string InventoryEvents     { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.InventoryEvents;
    public string OrderEvents         { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.OrderEvents;
    public string PaymentEvents       { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.PaymentEvents;
    public string ReviewEvents        { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.ReviewEvents;
    public string Notifications       { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.Notifications;
    public string Audit               { get; set; } = Pawzaroo.Application.Common.Messaging.KafkaTopics.Audit;
}
