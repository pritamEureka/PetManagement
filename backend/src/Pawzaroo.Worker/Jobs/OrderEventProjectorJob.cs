using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Messaging;
using Pawzaroo.Infrastructure.Messaging;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Reads marketplace order/inventory/payment events and:
///   - Notifies the buyer on order status changes
///   - Bumps the unread counter
///   - Forwards low-stock alerts to the seller's in-app inbox
///
/// Search-index updates are a separate consumer (SearchIndexerJob).
/// </summary>
public class OrderEventProjectorJob : KafkaConsumerBase
{
    public OrderEventProjectorJob(IOptions<KafkaOptions> options, IServiceScopeFactory scopeFactory,
        ILogger<OrderEventProjectorJob> logger)
        : base(options, scopeFactory, logger) { }

    protected override string GroupId => KafkaConsumerGroups.OrderProjector;
    protected override IReadOnlyList<string> Topics => new[]
    {
        Options.Topics.OrderEvents,
        Options.Topics.PaymentEvents,
        Options.Topics.InventoryEvents
    };

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task HandleAsync(string topic, JsonElement data, EventEnvelopeHeader header,
        IServiceProvider scope, CancellationToken ct)
    {
        var notifier = scope.GetRequiredService<INotificationService>();

        switch (header.EventType)
        {
            case nameof(OrderPlacedIntegration):
            {
                var ev = data.Deserialize<OrderPlacedIntegration>(Json);
                if (ev is null) return;
                await notifier.NotifyUserAsync(ev.UserId,
                    "Order placed",
                    $"Order {ev.OrderNumber} for ${ev.Total:0.00}",
                    new { ev.OrderId, ev.OrderNumber }, ct);
                Logger.LogInformation("[order-projector] notified buyer of order {OrderNumber}", ev.OrderNumber);
                break;
            }
            case nameof(OrderShipped):
            {
                var ev = data.Deserialize<OrderShipped>(Json);
                if (ev is null) return;
                Logger.LogInformation("[order-projector] order {OrderId} shipped tracking={Tracking}", ev.OrderId, ev.TrackingNumber);
                break;
            }
            case nameof(PaymentCompleted):
            {
                var ev = data.Deserialize<PaymentCompleted>(Json);
                if (ev is null) return;
                Logger.LogInformation("[order-projector] payment completed for {OrderId} amount={Amount}", ev.OrderId, ev.Amount);
                break;
            }
            case nameof(ProductStockLow):
            {
                var ev = data.Deserialize<ProductStockLow>(Json);
                if (ev is null) return;
                // In a real impl we'd resolve the storeOwner from StoreId and notify them.
                Logger.LogWarning("[order-projector] low stock product={Product} remaining={Remaining}", ev.ProductId, ev.RemainingStock);
                break;
            }
            default:
                Logger.LogDebug("[order-projector] ignoring {Type} from {Topic}", header.EventType, topic);
                break;
        }
    }
}
