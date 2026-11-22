using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Infrastructure.Modules.Push;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// UX package: notification mutes + digest preferences.
/// The worker job <see cref="NotificationDigestJob"/> is NOT registered here (the API must not run it):
/// add <c>services.AddHostedService&lt;NotificationDigestJob&gt;()</c> in the Worker host.
/// </summary>
public static class ExperiencePlusRegistration
{
    public static IServiceCollection AddExperiencePlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<INotificationMuteService, NotificationMuteService>();
        return s;
    }
}
