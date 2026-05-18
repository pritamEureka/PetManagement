using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Audit;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Identity;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Infrastructure.Storage;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Postgres"),
                    npg => npg.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
               .UseSnakeCaseNamingConvention());
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            RedisConnection.Build(config, sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<CacheHelper>();
        services.AddSingleton<IRedisCacheService, RedisCacheService>();
        services.AddSingleton<IPermissionCache, PermissionCache>();
        services.AddSingleton<ISessionCache, SessionCache>();
        services.AddSingleton<IOtpCache, OtpCache>();
        services.AddSingleton<INotificationCountCache, NotificationCountCache>();
        services.AddSingleton<IRedisRateLimiter, RedisRateLimiter>();
        services.AddSingleton<IDoctorAvailabilityCache, DoctorAvailabilityCache>();
        services.AddSingleton<IFeedCache, Modules.Feed.FeedCache>();

        // Feed module services
        services.AddScoped<Application.Modules.Feed.Services.IFeedService,           Modules.Feed.FeedService>();
        services.AddScoped<Application.Modules.Feed.Services.IFeedReactionService,   Modules.Feed.FeedReactionService>();
        services.AddScoped<Application.Modules.Feed.Services.IFeedCommentService,    Modules.Feed.FeedCommentService>();
        services.AddScoped<Application.Modules.Feed.Services.IFeedSavedPostService,  Modules.Feed.FeedSavedPostService>();
        services.AddScoped<Application.Modules.Feed.Services.IFeedReportService,     Modules.Feed.FeedReportService>();
        services.AddScoped<Application.Modules.Feed.Services.IFeedShareService,      Modules.Feed.FeedShareService>();

        // Adoption module services
        services.AddScoped<Application.Modules.Adoption.Services.IAdoptionListingService,    Modules.Adoption.AdoptionListingService>();
        services.AddScoped<Application.Modules.Adoption.Services.IAdoptionRequestService,    Modules.Adoption.AdoptionRequestService>();
        services.AddScoped<Application.Modules.Adoption.Services.ISavedAdoptionService,      Modules.Adoption.SavedAdoptionService>();
        services.AddScoped<Application.Modules.Adoption.Services.IAdoptionWantedPostService, Modules.Adoption.AdoptionWantedPostService>();

        // Messaging module services
        services.AddSingleton<IPresenceService, Modules.Messaging.PresenceService>();
        services.AddScoped<Application.Modules.Messaging.Services.IMessagingService,         Modules.Messaging.MessagingService>();
        services.AddScoped<Application.Modules.Messaging.Services.IMessageModerationService, Modules.Messaging.MessageModerationService>();

        // Vet module services
        services.AddSingleton<Application.Modules.Vet.Services.ISlotLockService,          Modules.Vet.SlotLockService>();
        services.AddScoped<Application.Modules.Vet.Services.IVetDoctorService,            Modules.Vet.VetDoctorService>();
        services.AddScoped<Application.Modules.Vet.Services.IDoctorAvailabilityService,   Modules.Vet.DoctorAvailabilityService>();
        services.AddScoped<Application.Modules.Vet.Services.IAppointmentService,          Modules.Vet.AppointmentService>();
        services.AddScoped<Application.Modules.Vet.Services.IPrescriptionService,         Modules.Vet.PrescriptionService>();
        services.AddScoped<Application.Modules.Vet.Services.IDoctorReviewService,         Modules.Vet.DoctorReviewService>();

        // Marketplace module services
        services.AddSingleton<Application.Modules.Marketplace.Services.IMarketplaceCache,            Modules.Marketplace.MarketplaceCache>();
        services.AddScoped<Application.Modules.Marketplace.Services.IStoreOwnerProfileService,       Modules.Marketplace.StoreOwnerProfileService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IStoreService,                   Modules.Marketplace.StoreService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IProductCategoryService,         Modules.Marketplace.ProductCategoryService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IProductService,                 Modules.Marketplace.ProductService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IInventoryService,               Modules.Marketplace.InventoryService>();
        services.AddScoped<Application.Modules.Marketplace.Services.ICartService,                    Modules.Marketplace.CartService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IOrderService,                   Modules.Marketplace.OrderService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IProductReviewService,           Modules.Marketplace.ProductReviewService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IStoreReviewService,             Modules.Marketplace.StoreReviewService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IShippingAddressService,         Modules.Marketplace.ShippingAddressService>();
        services.AddScoped<Application.Modules.Marketplace.Services.ICommissionConfigurationService, Modules.Marketplace.CommissionConfigurationService>();
        services.AddScoped<Application.Modules.Marketplace.Services.IReturnService,                  Modules.Marketplace.ReturnService>();
        services.AddScoped<Application.Modules.Marketplace.Services.ISalesReportService,             Modules.Marketplace.SalesReportService>();

        services.Configure<KafkaOptions>(config.GetSection("Kafka"));
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddSingleton<Application.Common.Messaging.IConsumerIdempotency, RedisConsumerIdempotency>();
        services.AddScoped<Application.Common.Messaging.IOutbox, Outbox>();

        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // Security / moderation module
        services.AddScoped<Application.Modules.Security.Services.IReportService,         Modules.Security.ReportService>();
        services.AddScoped<Application.Modules.Security.Services.IModerationService,     Modules.Security.ModerationService>();
        services.AddScoped<Application.Modules.Security.Services.IUserDeviceService,     Modules.Security.UserDeviceService>();
        services.AddScoped<Application.Modules.Security.Services.IUserDisciplineService, Modules.Security.UserDisciplineService>();
        services.AddScoped<Application.Modules.Security.Services.IAdminActionLogger,     Modules.Security.AdminActionLogger>();
        services.AddScoped<Application.Modules.Security.Services.IOtpService,            Modules.Security.OtpService>();
        services.AddScoped<Application.Modules.Security.Services.ITwoFactorService,      Modules.Security.TwoFactorService>();
        services.AddSingleton<Application.Modules.Security.Services.IFileValidationService, Modules.Security.FileValidationService>();
        // Dev: log-only OTP transport. Swap in SES/Twilio adapter for prod.
        services.AddScoped<Application.Modules.Security.Services.IOtpDeliveryService,    Modules.Security.ConsoleOtpDelivery>();

        // Generic transactional email (registration approval, etc.) — log + in-app
        // notification fallback. Swap in MailKit/SendGrid for prod.
        services.AddScoped<IEmailService, Modules.Identity.ConsoleEmailService>();

        return services;
    }

    /// <summary>API-specific bits (in-proc Kafka consumer). Worker doesn't use this.</summary>
    public static IServiceCollection AddApiKafkaConsumers(this IServiceCollection services)
    {
        services.AddHostedService<KafkaConsumerHostedService>();
        return services;
    }
}
