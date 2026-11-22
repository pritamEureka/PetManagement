using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

/// <summary>
/// Location-aware discovery. A post's effective location is its PostLocation row (city and/or ~1 km point)
/// or, failing that, the author's profile city/coordinates. Plain haversine; PostGIS is not required.
/// </summary>
public class NearbyFeedService : INearbyFeedService
{
    private const int ScanLimit = 300;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFeedService _feed;

    public NearbyFeedService(ApplicationDbContext db, ICurrentUserService current, IFeedService feed)
    {
        _db = db;
        _current = current;
        _feed = feed;
    }

    public async Task<CursorPage<NearbyFeedItemDto>> GetNearbyAsync(NearbyFeedQuery q, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var me = await _db.UserProfiles.AsNoTracking().Where(p => p.UserId == uid)
            .Select(p => new { p.City, p.Latitude, p.Longitude }).FirstOrDefaultAsync(ct);

        var lat = q.Lat ?? me?.Latitude;
        var lng = q.Lng ?? me?.Longitude;
        if (!GeoSearch.IsValidPoint(lat, lng)) { lat = null; lng = null; }
        var city = string.IsNullOrWhiteSpace(q.City) ? me?.City : q.City.Trim();
        if (lat is null && string.IsNullOrWhiteSpace(city)) return new CursorPage<NearbyFeedItemDto>(Array.Empty<NearbyFeedItemDto>(), null);
        var radius = Math.Clamp(q.RadiusKm ?? 25, 1, 200);
        var take = Math.Clamp(q.PageSize, 1, 50);

        var rows = from p in _db.Posts.AsNoTracking()
                   where !p.IsHidden
                   join l in _db.Set<PostLocation>().AsNoTracking() on p.Id equals l.PostId into ls
                   from l in ls.DefaultIfEmpty()
                   join pr in _db.UserProfiles.AsNoTracking() on p.AuthorId equals pr.UserId into prs
                   from pr in prs.DefaultIfEmpty()
                   select new
                   {
                       p.Id, p.CreatedAt,
                       City = l != null && l.City != null ? l.City : (pr != null ? pr.City : null),
                       Lat = l != null && l.Latitude != null ? l.Latitude : (pr != null ? pr.Latitude : null),
                       Lng = l != null && l.Longitude != null ? l.Longitude : (pr != null ? pr.Longitude : null)
                   };

        if (lat is { } la && lng is { } lo)
        {
            var (dLat, dLng) = GeoSearch.BoundingBox(la, radius);
            double minLat = la - dLat, maxLat = la + dLat, minLng = lo - dLng, maxLng = lo + dLng;
            var c = city;
            rows = rows.Where(r => (r.Lat != null && r.Lat >= minLat && r.Lat <= maxLat && r.Lng >= minLng && r.Lng <= maxLng)
                || (r.Lat == null && c != null && r.City != null && EF.Functions.ILike(r.City, c)));
        }
        else
        {
            var like = $"%{city}%";
            rows = rows.Where(r => r.City != null && EF.Functions.ILike(r.City, like));
        }

        var cur = FeedCursor.Decode(q.Cursor);
        if (cur is { } k) rows = rows.Where(r => r.CreatedAt < k.CreatedAt || (r.CreatedAt == k.CreatedAt && r.Id.CompareTo(k.Id) < 0));

        var scanned = await rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(ScanLimit).ToListAsync(ct);
        var matched = new List<(Guid Id, DateTime At, double? Km, string? City)>();
        foreach (var r in scanned)
        {
            double? km = null;
            if (lat is { } a && lng is { } o && r.Lat is { } rl && r.Lng is { } ro)
            {
                km = GeoSearch.DistanceKm(a, o, rl, ro);
                if (km > radius) continue;
            }
            matched.Add((r.Id, r.CreatedAt, km, r.City));
            if (matched.Count > take) break;
        }

        string? next = null;
        if (matched.Count > take)
        {
            matched.RemoveAt(matched.Count - 1);
            var last = matched[^1];
            next = FeedCursor.Encode(last.At, last.Id);
        }

        var items = new List<NearbyFeedItemDto>();
        foreach (var m in matched)
        {
            var post = await _feed.GetByIdAsync(m.Id, ct);
            if (post is not null) items.Add(new NearbyFeedItemDto(post, m.Km is { } d ? Math.Round(d, 1) : null, m.City));
        }
        return new CursorPage<NearbyFeedItemDto>(items, next);
    }
}
