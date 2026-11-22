using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Infrastructure.Modules.Communities;
using Pawzaroo.Infrastructure.Modules.Push;

namespace Pawzaroo.Infrastructure.Features;

public static class CommunityPlusRegistration
{
    /// <summary>Registers roadmap 8.6 (communities/events) and 8.9 (web push + notification preferences).</summary>
    public static IServiceCollection AddCommunityPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<ICommunityService, CommunityService>();

        s.AddScoped<IPushSubscriptionService, PushSubscriptionService>();
        s.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        s.AddScoped<IPushDispatcher, PushDispatcher>();
        // TryAdd so a real sender registered earlier wins; a later AddSingleton also overrides this one.
        // Runtime selection: real VAPID sender when Push:VapidPublicKey/VapidPrivateKey are valid, else log-only.
        s.AddHttpClient(VapidWebPushSender.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
        s.TryAddSingleton<IWebPushSender>(WebPushSenderFactory.Create);
        return s;
    }
}
