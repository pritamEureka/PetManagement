using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Infrastructure.Modules.ShopPlus;

namespace Pawzaroo.Infrastructure.Features;

public static class ShopPlusRegistration
{
    /// <summary>
    /// Registers roadmap 8.5 (variants, reservations, subscriptions, reorder) and 8.7 (discovery) services.
    /// Call it AFTER ICurrentUserService has been registered (API: after AddInfrastructure/its HTTP user service;
    /// Worker: after the SystemCurrentUserService line) — it decorates ICurrentUserService so the worker can
    /// place orders on behalf of a subscriber through the public IOrderService.
    /// </summary>
    public static IServiceCollection AddShopPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<ShopPlusUserContext>();
        DecorateCurrentUser(s);

        s.AddScoped<IProductVariantService, ProductVariantService>();
        s.AddScoped<IStockReservationService, StockReservationService>();
        s.AddScoped<ProductSubscriptionService>();
        s.AddScoped<IProductSubscriptionService>(sp => sp.GetRequiredService<ProductSubscriptionService>());
        s.AddScoped<IReorderService>(sp => sp.GetRequiredService<ProductSubscriptionService>());
        s.AddScoped<ISubscriptionRunner, SubscriptionRunner>();

        s.AddScoped<IMarketplaceDiscoveryService, MarketplaceDiscoveryService>();
        s.AddScoped<IDiscoveryAlertProcessor, DiscoveryAlertProcessor>();
        return s;
    }

    private static void DecorateCurrentUser(IServiceCollection s)
    {
        var existing = s.LastOrDefault(d => d.ServiceType == typeof(ICurrentUserService) && !d.IsKeyedService);
        if (existing is null) return;
        if (existing.ImplementationType == typeof(OverridableCurrentUserService)) return; // already decorated
        s.Remove(existing);
        s.Add(ServiceDescriptor.Describe(typeof(ICurrentUserService), sp =>
        {
            var inner = (ICurrentUserService)(existing.ImplementationInstance
                ?? existing.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!));
            return new OverridableCurrentUserService(inner, sp.GetRequiredService<ShopPlusUserContext>());
        }, ServiceLifetime.Scoped));
    }
}
