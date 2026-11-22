using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Communities;

/// <summary>
/// Pet communities (breed/species groups), membership + moderation, and RSVP-able local events.
/// Posts are intentionally out of scope. Entities are soft-deleted via the global filter, but
/// membership/RSVP rows that must be re-creatable are hard-deleted with ExecuteDelete.
/// </summary>
public class CommunityService : ICommunityService
{
    private const int MaxDiscoveryScan = 500;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationCountCache _counter;

    public CommunityService(ApplicationDbContext db, ICurrentUserService current, INotificationCountCache counter)
    {
        _db = db;
        _current = current;
        _counter = counter;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });

    private static DateTime Utc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private static bool IsModerator(CommunityMember? m) =>
        m is { Status: CommunityMemberStatus.Active, Role: CommunityRole.Moderator or CommunityRole.Owner };

    private IQueryable<CommunityMember> Members => _db.Set<CommunityMember>();

    private async Task<Community> RequireCommunity(Guid id, CancellationToken ct) =>
        await _db.Set<Community>().FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Community", id);

    private Task<CommunityMember?> MembershipAsync(Guid communityId, Guid userId, CancellationToken ct) =>
        Members.FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == userId, ct);

    // ---------------------------------------------------------------- communities

    public async Task<CommunityPage<CommunityDto>> ListAsync(CommunityQuery q, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 50);
        var query = _db.Set<Community>().AsNoTracking().AsQueryable();

        if (q.Mine)
        {
            var me = Uid();
            var mine = Members.Where(m => m.UserId == me && m.Status != CommunityMemberStatus.Banned).Select(m => m.CommunityId);
            query = query.Where(c => mine.Contains(c.Id));
        }
        if (!string.IsNullOrWhiteSpace(q.Species))
        {
            var s = q.Species.Trim();
            query = query.Where(c => c.Species != null && EF.Functions.ILike(c.Species, s));
        }
        if (!string.IsNullOrWhiteSpace(q.City))
        {
            var city = $"%{q.City.Trim()}%";
            query = query.Where(c => c.City != null && EF.Functions.ILike(c.City, city));
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = $"%{q.Q.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, term)
                || (c.Breed != null && EF.Functions.ILike(c.Breed, term))
                || (c.Species != null && EF.Functions.ILike(c.Species, term))
                || (c.Description != null && EF.Functions.ILike(c.Description, term)));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CommunityPage<CommunityDto>(await ToDtosAsync(rows, uid, ct), total, page, size);
    }

    public async Task<CommunityDto> GetAsync(string idOrSlug, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var query = _db.Set<Community>().AsNoTracking();
        var c = Guid.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            : await query.FirstOrDefaultAsync(x => x.Slug == idOrSlug.ToLower(), ct);
        if (c is null) throw new NotFoundException("Community", idOrSlug);
        return (await ToDtosAsync(new List<Community> { c }, uid, ct))[0];
    }

    public async Task<CommunityDto> CreateAsync(CreateCommunityRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var name = req.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120) throw Invalid("name", "Name is required (max 120 characters).");

        var community = new Community
        {
            Name = name,
            Slug = await UniqueSlugAsync(name, ct),
            Species = Clean(req.Species, 60),
            Breed = Clean(req.Breed, 80),
            IsPublic = req.IsPublic,
            Description = Clean(req.Description, 2000),
            City = Clean(req.City, 100),
            OwnerId = uid,
        };
        _db.Set<Community>().Add(community);
        _db.Set<CommunityMember>().Add(new CommunityMember
        {
            CommunityId = community.Id, UserId = uid,
            Role = CommunityRole.Owner, Status = CommunityMemberStatus.Active,
        });
        await _db.SaveChangesAsync(ct);
        return (await ToDtosAsync(new List<Community> { community }, uid, ct))[0];
    }

    public async Task<CommunityDto> JoinAsync(Guid communityId, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = await RequireCommunity(communityId, ct);
        var existing = await MembershipAsync(communityId, uid, ct);
        if (existing is { Status: CommunityMemberStatus.Banned }) throw new ForbiddenException("You are banned from this community.");
        if (existing is null)
        {
            _db.Set<CommunityMember>().Add(new CommunityMember
            {
                CommunityId = communityId, UserId = uid, Role = CommunityRole.Member,
                Status = c.IsPublic ? CommunityMemberStatus.Active : CommunityMemberStatus.Pending,
            });
            await _db.SaveChangesAsync(ct);
        }
        return (await ToDtosAsync(new List<Community> { c }, uid, ct))[0];
    }

    public async Task LeaveAsync(Guid communityId, CancellationToken ct = default)
    {
        var uid = Uid();
        var m = await MembershipAsync(communityId, uid, ct);
        if (m is null) return;
        if (m.Role == CommunityRole.Owner) throw new ConflictException("The owner cannot leave the community.");
        if (m.Status == CommunityMemberStatus.Banned) throw new ForbiddenException("You are banned from this community.");
        await Members.Where(x => x.Id == m.Id).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<CommunityMemberDto>> ListMembersAsync(Guid communityId, CommunityMemberStatus? status, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = await RequireCommunity(communityId, ct);
        var mine = await MembershipAsync(communityId, uid, ct);
        var active = mine is { Status: CommunityMemberStatus.Active };
        if (!c.IsPublic && !active) throw new ForbiddenException("Members only.");
        var wanted = status ?? CommunityMemberStatus.Active;
        if (wanted != CommunityMemberStatus.Active && !IsModerator(mine)) throw new ForbiddenException("Moderators only.");

        var rows = await (from m in Members.AsNoTracking()
                          join u in _db.Set<User>().AsNoTracking() on m.UserId equals u.Id
                          where m.CommunityId == communityId && m.Status == wanted
                          orderby m.Role descending, m.JoinedAt
                          select new CommunityMemberDto(u.Id, u.DisplayName, u.AvatarUrl, m.Role, m.Status, m.JoinedAt))
            .Take(500).ToListAsync(ct);
        return rows;
    }

    public async Task ModerateAsync(Guid communityId, Guid targetUserId, MemberAction action, CancellationToken ct = default)
    {
        var uid = Uid();
        await RequireCommunity(communityId, ct);
        var actor = await MembershipAsync(communityId, uid, ct);
        if (!IsModerator(actor)) throw new ForbiddenException("Moderators only.");
        var actorIsOwner = actor!.Role == CommunityRole.Owner;

        var target = await MembershipAsync(communityId, targetUserId, ct) ?? throw new NotFoundException("Member", targetUserId);
        if (target.Role == CommunityRole.Owner) throw new ForbiddenException("The owner cannot be moderated.");
        if (targetUserId == uid) throw new ForbiddenException("You cannot moderate yourself.");
        var targetIsMod = target.Role == CommunityRole.Moderator;
        if (targetIsMod && !actorIsOwner) throw new ForbiddenException("Only the owner can moderate moderators.");

        switch (action)
        {
            case MemberAction.Approve:
                if (target.Status != CommunityMemberStatus.Pending) throw new ConflictException("Member is not pending.");
                target.Status = CommunityMemberStatus.Active;
                break;
            case MemberAction.Ban:
                target.Status = CommunityMemberStatus.Banned;
                target.Role = CommunityRole.Member;
                break;
            case MemberAction.Unban:
                if (target.Status != CommunityMemberStatus.Banned) throw new ConflictException("Member is not banned.");
                target.Status = CommunityMemberStatus.Active;
                break;
            case MemberAction.Promote:
                if (!actorIsOwner) throw new ForbiddenException("Only the owner can promote members.");
                if (target.Status != CommunityMemberStatus.Active) throw new ConflictException("Member is not active.");
                target.Role = CommunityRole.Moderator;
                break;
            case MemberAction.Demote:
                if (!actorIsOwner) throw new ForbiddenException("Only the owner can demote moderators.");
                if (!targetIsMod) throw new ConflictException("Member is not a moderator.");
                target.Role = CommunityRole.Member;
                break;
            case MemberAction.Remove:
                await Members.Where(x => x.Id == target.Id).ExecuteDeleteAsync(ct);
                return;
            default:
                throw Invalid("action", "Unknown action.");
        }
        await _db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- events

    public async Task<CommunityEventDto> CreateEventAsync(Guid communityId, CreateCommunityEventRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        await RequireCommunity(communityId, ct);
        var m = await MembershipAsync(communityId, uid, ct);
        if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Only active members can host events.");

        var title = req.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 160) throw Invalid("title", "Title is required (max 160 characters).");
        var starts = Utc(req.StartsAt);
        var ends = Utc(req.EndsAt);
        if (ends <= starts) throw Invalid("endsAt", "End must be after start.");
        if (starts < DateTime.UtcNow.AddMinutes(-5)) throw Invalid("startsAt", "Start must be in the future.");
        if (req.Capacity is < 1) throw Invalid("capacity", "Capacity must be at least 1.");
        if ((req.Latitude is null) != (req.Longitude is null) ||
            (req.Latitude is not null && !GeoSearch.IsValidPoint(req.Latitude, req.Longitude)))
            throw Invalid("latitude", "Provide a valid latitude and longitude together.");

        var ev = new CommunityEvent
        {
            CommunityId = communityId, OrganizerId = uid, Title = title,
            Description = Clean(req.Description, 4000), Type = req.Type,
            StartsAt = starts, EndsAt = ends,
            City = Clean(req.City, 100), Venue = Clean(req.Venue, 200),
            Latitude = req.Latitude, Longitude = req.Longitude, Capacity = req.Capacity,
        };
        _db.Set<CommunityEvent>().Add(ev);
        // The organizer is automatically going.
        _db.Set<EventRsvp>().Add(new EventRsvp { EventId = ev.Id, UserId = uid, Status = RsvpStatus.Going });
        await _db.SaveChangesAsync(ct);
        return (await ToEventDtosAsync(new List<CommunityEvent> { ev }, uid, null, ct))[0];
    }

    public async Task<IReadOnlyList<CommunityEventDto>> ListCommunityEventsAsync(Guid communityId, bool includePast, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var c = await RequireCommunity(communityId, ct);
        if (!c.IsPublic)
        {
            var m = uid is { } u ? await MembershipAsync(communityId, u, ct) : null;
            if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Members only.");
        }
        var now = DateTime.UtcNow;
        var q = _db.Set<CommunityEvent>().AsNoTracking().Where(e => e.CommunityId == communityId);
        if (!includePast) q = q.Where(e => e.EndsAt >= now);
        var rows = await q.OrderBy(e => e.StartsAt).Take(200).ToListAsync(ct);
        return await ToEventDtosAsync(rows, uid, null, ct);
    }

    public async Task<CommunityPage<CommunityEventDto>> DiscoverEventsAsync(EventDiscoveryQuery q, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 50);
        var now = DateTime.UtcNow;
        var fromUtc = q.From is { } f ? Utc(f) : now;

        var myCommunityIds = uid is { } u
            ? await Members.Where(m => m.UserId == u && m.Status == CommunityMemberStatus.Active).Select(m => m.CommunityId).ToListAsync(ct)
            : new List<Guid>();

        var query = from ev in _db.Set<CommunityEvent>().AsNoTracking()
                    join c in _db.Set<Community>().AsNoTracking() on ev.CommunityId equals c.Id
                    where !ev.IsCancelled && ev.EndsAt >= fromUtc && (c.IsPublic || myCommunityIds.Contains(c.Id))
                    select ev;
        if (q.To is { } to) { var t = Utc(to); query = query.Where(e => e.StartsAt <= t); }
        if (q.Type is { } type) query = query.Where(e => e.Type == type);
        if (!string.IsNullOrWhiteSpace(q.City))
        {
            var city = $"%{q.City.Trim()}%";
            query = query.Where(e => e.City != null && EF.Functions.ILike(e.City, city));
        }

        if (q.Lat is { } lat && q.Lng is { } lng && GeoSearch.IsValidPoint(lat, lng))
        {
            var radius = Math.Clamp(q.RadiusKm ?? 25, 1, 500);
            var (dLat, dLng) = GeoSearch.BoundingBox(lat, radius);
            double minLat = lat - dLat, maxLat = lat + dLat, minLng = lng - dLng, maxLng = lng + dLng;
            var candidates = await query
                .Where(e => e.Latitude != null && e.Longitude != null
                    && e.Latitude >= minLat && e.Latitude <= maxLat
                    && e.Longitude >= minLng && e.Longitude <= maxLng)
                .OrderBy(e => e.StartsAt).Take(MaxDiscoveryScan).ToListAsync(ct);

            var withDistance = candidates
                .Select(e => (Event: e, Km: GeoSearch.DistanceKm(lat, lng, e.Latitude!.Value, e.Longitude!.Value)))
                .Where(x => x.Km <= radius)
                .OrderBy(x => x.Km).ThenBy(x => x.Event.StartsAt)
                .ToList();
            var slice = withDistance.Skip((page - 1) * size).Take(size).ToList();
            var distances = slice.ToDictionary(x => x.Event.Id, x => x.Km);
            var dtos = await ToEventDtosAsync(slice.Select(x => x.Event).ToList(), uid, distances, ct);
            return new CommunityPage<CommunityEventDto>(dtos, withDistance.Count, page, size);
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(e => e.StartsAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CommunityPage<CommunityEventDto>(await ToEventDtosAsync(rows, uid, null, ct), total, page, size);
    }

    public async Task<CommunityPage<CommunityEventDto>> GroupEventFeedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        page = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 50);
        var now = DateTime.UtcNow;
        var mine = Members.Where(m => m.UserId == uid && m.Status == CommunityMemberStatus.Active).Select(m => m.CommunityId);
        var query = _db.Set<CommunityEvent>().AsNoTracking()
            .Where(e => !e.IsCancelled && e.EndsAt >= now && mine.Contains(e.CommunityId));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(e => e.StartsAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new CommunityPage<CommunityEventDto>(await ToEventDtosAsync(rows, uid, null, ct), total, page, size);
    }

    public async Task<CommunityEventDto> GetEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var ev = await _db.Set<CommunityEvent>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
        var c = await RequireCommunity(ev.CommunityId, ct);
        if (!c.IsPublic)
        {
            var m = uid is { } u ? await MembershipAsync(c.Id, u, ct) : null;
            if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Members only.");
        }
        return (await ToEventDtosAsync(new List<CommunityEvent> { ev }, uid, null, ct))[0];
    }

    public async Task<CommunityEventDto> RsvpAsync(Guid eventId, CancellationToken ct = default)
    {
        var uid = Uid();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockEventAsync(eventId, ct);

        var ev = await _db.Set<CommunityEvent>().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
        if (ev.IsCancelled) throw new ConflictException("This event was cancelled.");
        if (ev.EndsAt < DateTime.UtcNow) throw new ConflictException("This event has already ended.");
        var m = await MembershipAsync(ev.CommunityId, uid, ct);
        if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Join the community to RSVP.");

        var rsvp = await _db.Set<EventRsvp>().FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == uid, ct);
        if (rsvp is { Status: RsvpStatus.Going or RsvpStatus.Waitlisted })
        {
            await tx.CommitAsync(ct);
            return (await ToEventDtosAsync(new List<CommunityEvent> { ev }, uid, null, ct))[0];
        }

        var going = await _db.Set<EventRsvp>().CountAsync(r => r.EventId == eventId && r.Status == RsvpStatus.Going, ct);
        var status = ev.Capacity is null || going < ev.Capacity ? RsvpStatus.Going : RsvpStatus.Waitlisted;
        if (rsvp is null)
        {
            _db.Set<EventRsvp>().Add(new EventRsvp { EventId = eventId, UserId = uid, Status = status, RsvpAt = DateTime.UtcNow });
        }
        else
        {
            rsvp.Status = status;
            rsvp.RsvpAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToEventDtosAsync(new List<CommunityEvent> { ev }, uid, null, ct))[0];
    }

    public async Task<CommunityEventDto> CancelRsvpAsync(Guid eventId, CancellationToken ct = default)
    {
        var uid = Uid();
        var promoted = new List<Guid>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockEventAsync(eventId, ct);

        var ev = await _db.Set<CommunityEvent>().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
        var rsvp = await _db.Set<EventRsvp>().FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == uid, ct);
        if (rsvp is { Status: RsvpStatus.Going or RsvpStatus.Waitlisted })
        {
            var wasGoing = rsvp.Status == RsvpStatus.Going;
            rsvp.Status = RsvpStatus.Cancelled;
            await _db.SaveChangesAsync(ct);

            if (wasGoing)
            {
                // Auto-promote the longest-waiting people while seats are free.
                while (true)
                {
                    var going = await _db.Set<EventRsvp>().CountAsync(r => r.EventId == eventId && r.Status == RsvpStatus.Going, ct);
                    if (ev.Capacity is { } cap && going >= cap) break;
                    var next = await _db.Set<EventRsvp>()
                        .Where(r => r.EventId == eventId && r.Status == RsvpStatus.Waitlisted)
                        .OrderBy(r => r.RsvpAt).FirstOrDefaultAsync(ct);
                    if (next is null) break;
                    next.Status = RsvpStatus.Going;
                    promoted.Add(next.UserId);
                    await _db.SaveChangesAsync(ct);
                }
                foreach (var p in promoted)
                    AddNotification(p, "You're in!", $"A spot opened up for \"{ev.Title}\" and you are now going.", "/communities/events");
                if (promoted.Count > 0) await _db.SaveChangesAsync(ct);
            }
        }
        await tx.CommitAsync(ct);
        foreach (var p in promoted) await BumpAsync(p, ct);
        return (await ToEventDtosAsync(new List<CommunityEvent> { ev }, uid, null, ct))[0];
    }

    public async Task CancelEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var uid = Uid();
        var ev = await _db.Set<CommunityEvent>().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
        var m = await MembershipAsync(ev.CommunityId, uid, ct);
        if (ev.OrganizerId != uid && !IsModerator(m)) throw new ForbiddenException();
        if (ev.IsCancelled) return;
        ev.IsCancelled = true;

        var attendees = await _db.Set<EventRsvp>()
            .Where(r => r.EventId == eventId && r.Status != RsvpStatus.Cancelled && r.UserId != uid)
            .Select(r => r.UserId).Take(500).ToListAsync(ct);
        foreach (var a in attendees)
            AddNotification(a, "Event cancelled", $"\"{ev.Title}\" has been cancelled.", "/communities/events");
        await _db.SaveChangesAsync(ct);
        foreach (var a in attendees) await BumpAsync(a, ct);
    }

    public async Task<IReadOnlyList<EventAttendeeDto>> ListAttendeesAsync(Guid eventId, CancellationToken ct = default)
    {
        var uid = Uid();
        var ev = await _db.Set<CommunityEvent>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new NotFoundException("Event", eventId);
        var m = await MembershipAsync(ev.CommunityId, uid, ct);
        if (ev.OrganizerId != uid && !IsModerator(m)) throw new ForbiddenException();

        return await (from r in _db.Set<EventRsvp>().AsNoTracking()
                      join u in _db.Set<User>().AsNoTracking() on r.UserId equals u.Id
                      where r.EventId == eventId && r.Status != RsvpStatus.Cancelled
                      orderby r.Status, r.RsvpAt
                      select new EventAttendeeDto(u.Id, u.DisplayName, r.Status, r.RsvpAt))
            .Take(500).ToListAsync(ct);
    }

    // ---------------------------------------------------------------- helpers

    private Task LockEventAsync(Guid eventId, CancellationToken ct)
    {
        var key = eventId.ToString();
        return _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }

    private void AddNotification(Guid userId, string title, string body, string url) =>
        _db.Set<InAppNotification>().Add(new InAppNotification
        {
            UserId = userId, Title = title, Body = body, Url = url,
            Payload = "{\"type\":\"community_event\"}",
        });

    private async Task BumpAsync(Guid userId, CancellationToken ct)
    {
        try { await _counter.BumpUnreadAsync(userId, 1, ct); }
        catch { /* cache is best-effort; the DB count hydrates it */ }
    }

    private static string? Clean(string? s, int max)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var lastDash = true;
        foreach (var ch in name.ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(ch); lastDash = false; }
            else if (!lastDash) { sb.Append('-'); lastDash = true; }
        }
        var baseSlug = sb.ToString().Trim('-');
        if (baseSlug.Length == 0) baseSlug = "community";
        if (baseSlug.Length > 120) baseSlug = baseSlug[..120].Trim('-');

        var slug = baseSlug;
        for (var i = 2; await _db.Set<Community>().IgnoreQueryFilters().AnyAsync(c => c.Slug == slug, ct); i++)
            slug = $"{baseSlug}-{i}";
        return slug;
    }

    private async Task<List<CommunityDto>> ToDtosAsync(List<Community> rows, Guid? uid, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var counts = await Members.Where(m => ids.Contains(m.CommunityId) && m.Status == CommunityMemberStatus.Active)
            .GroupBy(m => m.CommunityId).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N, ct);
        var mine = uid is { } u
            ? await Members.Where(m => m.UserId == u && ids.Contains(m.CommunityId)).ToDictionaryAsync(m => m.CommunityId, ct)
            : new Dictionary<Guid, CommunityMember>();

        return rows.Select(c =>
        {
            mine.TryGetValue(c.Id, out var m);
            return new CommunityDto(c.Id, c.Name, c.Slug, c.Species, c.Breed, c.IsPublic, c.Description, c.City,
                c.OwnerId, counts.GetValueOrDefault(c.Id), m?.Role, m?.Status, c.CreatedAt);
        }).ToList();
    }

    private async Task<List<CommunityEventDto>> ToEventDtosAsync(
        List<CommunityEvent> rows, Guid? uid, Dictionary<Guid, double>? distances, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var cIds = rows.Select(r => r.CommunityId).Distinct().ToList();
        var names = await _db.Set<Community>().AsNoTracking().Where(c => cIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var counts = await _db.Set<EventRsvp>().AsNoTracking()
            .Where(r => ids.Contains(r.EventId) && r.Status != RsvpStatus.Cancelled)
            .GroupBy(r => new { r.EventId, r.Status }).Select(g => new { g.Key.EventId, g.Key.Status, N = g.Count() })
            .ToListAsync(ct);
        var mine = uid is { } u
            ? await _db.Set<EventRsvp>().AsNoTracking().Where(r => r.UserId == u && ids.Contains(r.EventId))
                .ToDictionaryAsync(r => r.EventId, r => r.Status, ct)
            : new Dictionary<Guid, RsvpStatus>();

        return rows.Select(e => new CommunityEventDto(
            e.Id, e.CommunityId, names.GetValueOrDefault(e.CommunityId, ""), e.OrganizerId, e.Title, e.Description,
            e.Type, e.StartsAt, e.EndsAt, e.City, e.Venue, e.Latitude, e.Longitude, e.Capacity, e.IsCancelled,
            counts.Where(c => c.EventId == e.Id && c.Status == RsvpStatus.Going).Select(c => c.N).FirstOrDefault(),
            counts.Where(c => c.EventId == e.Id && c.Status == RsvpStatus.Waitlisted).Select(c => c.N).FirstOrDefault(),
            mine.TryGetValue(e.Id, out var s) ? s : null,
            distances is not null && distances.TryGetValue(e.Id, out var km) ? Math.Round(km, 1) : null)).ToList();
    }
}
