using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Application.Modules.Feed;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Infrastructure.Modules.Communities;
using Pawzaroo.Infrastructure.Modules.Feed;
using Pawzaroo.Infrastructure.Modules.Pets;

namespace Pawzaroo.Infrastructure.Features;

public static class SocialPlusRegistration
{
    /// <summary>
    /// Community polls + reputation, pet albums/follows, nearby feed, lost &amp; found sightings/evidence/geofence alerts.
    /// Worker: register <c>LostFoundLocationExpiryJob</c> (Pawzaroo.Worker.Jobs) with AddHostedService.
    /// </summary>
    public static IServiceCollection AddSocialPlusFeatures(this IServiceCollection s)
    {
        s.AddScoped<IReputationService, ReputationService>();
        s.AddScoped<ICommunityPlusService, CommunityPlusService>();
        s.AddScoped<INearbyFeedService, NearbyFeedService>();
        s.AddScoped<IPetSocialService, PetSocialService>();
        s.AddScoped<ILostFoundPlusService, LostFoundPlusService>();
        return s;
    }
}
