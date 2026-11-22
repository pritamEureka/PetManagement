using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Modules.Messaging;

namespace Pawzaroo.Infrastructure.Features;

public static class AdoptionMessagingPlusRegistration
{
    /// <summary>
    /// Adoption pipeline/matching/risk + messaging search/pins/folders/disappearing/edit/E2EE registry/spam/coalescing.
    /// Call AFTER AddInfrastructure: it replaces the singleton IPresenceService with the resilient (Redis + in-memory
    /// fallback) implementation and exposes the same instance as ITypingStateService.
    /// The worker jobs (AdoptionSlaJob, DisappearingMessageJob) are NOT registered here (API must not run them):
    /// add them with AddHostedService in Worker/Program.cs.
    /// </summary>
    public static IServiceCollection AddAdoptionMessagingPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<IAdoptionPlusService, AdoptionPlusService>();

        s.AddScoped<IMessagingPlusService, MessagingPlusService>();
        s.AddScoped<IMessageSpamGuard, MessageSpamGuard>();
        s.AddScoped<MessageNotificationCoalescer>();

        s.Replace(ServiceDescriptor.Singleton<ResilientPresenceService, ResilientPresenceService>());
        s.Replace(ServiceDescriptor.Singleton<IPresenceService>(sp => sp.GetRequiredService<ResilientPresenceService>()));
        s.Replace(ServiceDescriptor.Singleton<ITypingStateService>(sp => sp.GetRequiredService<ResilientPresenceService>()));
        return s;
    }
}
