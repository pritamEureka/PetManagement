using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Feed;

/// <summary>Writes the optional PostLocation side row for a freshly created post. Never throws into post creation.</summary>
internal static class PostGeo
{
    public static async Task TagAsync(ApplicationDbContext db, Post post, CreatePostInput input, CancellationToken ct)
    {
        try
        {
            var (lat, lng) = GeoSearch.Approximate(input.Latitude, input.Longitude); // ~1 km, never the exact point
            var city = string.IsNullOrWhiteSpace(input.Location) ? null : input.Location.Trim();
            if (city is { Length: > 128 }) city = city[..128];
            if (city is null && lat is null)
                city = await db.UserProfiles.AsNoTracking().Where(p => p.UserId == post.AuthorId)
                    .Select(p => p.City).FirstOrDefaultAsync(ct);
            if (city is null && lat is null) return;
            db.Set<PostLocation>().Add(new PostLocation { PostId = post.Id, City = city, Latitude = lat, Longitude = lng });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Geo-tagging is best effort; the post itself is already saved.
        }
    }
}
