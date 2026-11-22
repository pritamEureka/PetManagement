using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Infrastructure.Modules.Marketplace;

namespace Pawzaroo.Infrastructure.Features;

public static class CommercePlusRegistration
{
    /// <summary>
    /// Delivery tracking (checkpoints, proof, courier webhooks) and the vendor settlement ledger.
    /// Must be registered for OrderService / DeliveryService to resolve (they depend on ILedgerRecorder).
    /// </summary>
    public static IServiceCollection AddCommercePlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<LedgerService>();
        s.AddScoped<ILedgerRecorder>(sp => sp.GetRequiredService<LedgerService>());
        s.AddScoped<ISettlementService>(sp => sp.GetRequiredService<LedgerService>());

        // DeliveryService also implements the system-level status applier used by tracking/webhooks.
        s.AddScoped<IDeliveryStatusApplier>(sp => (DeliveryService)sp.GetRequiredService<IDeliveryService>());
        s.AddScoped<IDeliveryTrackingService, DeliveryTrackingService>();
        s.AddScoped<ICourierWebhookService, CourierWebhookService>();
        return s;
    }
}
