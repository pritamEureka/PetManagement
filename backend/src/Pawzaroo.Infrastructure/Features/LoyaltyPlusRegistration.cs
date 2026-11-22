using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Infrastructure.Modules.LoyaltyPlus;

namespace Pawzaroo.Infrastructure.Features;

public static class LoyaltyPlusRegistration
{
    /// <summary>
    /// Marketplace expansion 1.4 / 2.4: bundles, low-stock alerts, replenishment, store comparison, delivery promise,
    /// availability check, promotions, loyalty/referral, gift cards, order pricing pipeline, sponsored placements.
    /// Call from BOTH the API and the Worker (the worker runs the alert/reminder/expiry jobs). OrderService takes
    /// IOrderPricingPipeline as an OPTIONAL ctor argument, so checkout is unchanged if this is not called.
    /// IDeliveryEtaContributor implementations (e.g. logistics) may be registered by anyone; all are summed.
    /// </summary>
    public static IServiceCollection AddLoyaltyPlusFeatures(this IServiceCollection s)
    {
        s.AddSingleton(sp => LoyaltyPlusOptions.From(sp.GetRequiredService<IConfiguration>()));

        s.AddScoped<IBundleService, BundleService>();
        s.AddScoped<IStockAlertService, StockAlertService>();
        s.AddScoped<IReplenishmentService, ReplenishmentService>();
        s.AddScoped<IStoreComparisonService, StoreComparisonService>();
        s.AddScoped<IDeliveryPromiseService, DeliveryPromiseService>();
        s.AddScoped<ICartAvailabilityService, CartAvailabilityService>();
        s.AddScoped<ISponsoredPlacementService, SponsoredPlacementService>();

        s.AddScoped<LoyaltyService>();
        s.AddScoped<ILoyaltyService>(sp => sp.GetRequiredService<LoyaltyService>());
        s.AddScoped<GiftCardService>();
        s.AddScoped<IGiftCardService>(sp => sp.GetRequiredService<GiftCardService>());
        s.AddScoped<IPromotionService, PromotionService>();

        s.AddScoped<IOrderPricingAdjuster, PromotionAdjuster>();
        s.AddScoped<IOrderPricingAdjuster, BundleAdjuster>();
        s.AddScoped<IOrderPricingAdjuster, LoyaltyRedemptionAdjuster>();
        s.AddScoped<IOrderPricingAdjuster, GiftCardAdjuster>();
        s.AddScoped<IOrderPricingPipeline, OrderPricingPipeline>();
        s.AddScoped<ICartPricingPreviewService, CartPricingPreviewService>();
        return s;
    }
}
