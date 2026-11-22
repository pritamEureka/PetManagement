using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.LogisticsPlus;

namespace Pawzaroo.Infrastructure.Features;

public static class LogisticsPlusRegistration
{
    /// <summary>
    /// Delivery zones + peak-hour quotes, fulfillment SLAs (and the SlaMonitor used by the worker job),
    /// return pickups, partial refunds / store split and seller metrics.
    /// Call from BOTH the API and the Worker (the worker needs ISlaMonitor).
    /// OrderService takes IShippingQuoteService as an OPTIONAL constructor argument, so checkout still works if this is not called.
    /// </summary>
    public static IServiceCollection AddLogisticsPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<IShippingQuoteService, ShippingQuoteService>();
        s.AddScoped<IDeliveryZoneAdminService, DeliveryZoneAdminService>();
        s.AddScoped<ISlaService, SlaService>();
        s.AddScoped<ISlaMonitor, SlaMonitor>();
        s.AddScoped<IReturnPickupService, ReturnPickupService>();
        s.AddScoped<IPartialRefundService, PartialRefundService>();
        s.AddScoped<IStoreMetricsService, StoreMetricsService>();
        s.AddScoped<Pawzaroo.Application.Modules.Marketplace.IDeliveryEtaContributor, LogisticsEtaContributor>();
        return s;
    }
}
