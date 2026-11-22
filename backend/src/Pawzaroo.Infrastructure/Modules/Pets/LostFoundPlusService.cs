using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>Sightings with owner verification, incident timeline, evidence and geofenced alert subscriptions.</summary>
public class LostFoundPlusService : ILostFoundPlusService
{
    private const int MaxActiveSubscriptions = 5;
    private const int MaxPendingSightingsPerUser = 5;
    private const int MaxEvidencePerReport = 30;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public LostFoundPlusService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool IsModerator => _current.Permissions.Contains(Permissions.LostFound.Moderate);

    private async Task<LostFoundReport> ReportAsync(Guid id, CancellationToken ct) =>
        await _db.LostFoundReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Report", id);

    private bool CanSeeAll(LostFoundReport r) => r.ReporterId == _current.UserId || IsModerator;

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });

    // ---------------------------------------------------------------- sightings

    public async Task<SightingDto> AddSightingAsync(Guid reportId, CreateSightingInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await ReportAsync(reportId, ct);
        if (r.Status != LostFoundStatus.Open) throw new ConflictException("This report is closed.");
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 1000)
            throw Invalid("description", "Describe what you saw (max 1000 characters).");
        if ((input.Latitude is null) != (input.Longitude is null)
            || (input.Latitude is not null && !GeoSearch.IsValidPoint(input.Latitude, input.Longitude)))
            throw Invalid("latitude", "Provide a valid latitude and longitude together.");
        var seen = Utc(input.SeenAt);
        if (seen > DateTime.UtcNow.AddHours(1)) throw Invalid("seenAt", "Sighting time can't be in the future.");
        if (input.PhotoUrl is { Length: > 0 } p && !Uri.TryCreate(p, UriKind.Absolute, out _))
            throw Invalid("photoUrl", "Invalid photo URL.");

        var pending = await _db.Set<LostFoundSighting>().CountAsync(s => s.ReportId == reportId && s.ReporterUserId == uid
            && s.Status == SightingStatus.Pending, ct);
        if (pending >= MaxPendingSightingsPerUser) throw new ConflictException("You have too many unreviewed sightings on this report.");

        var s = new LostFoundSighting
        {
            ReportId = reportId, ReporterUserId = uid, SeenAt = seen, Latitude = input.Latitude, Longitude = input.Longitude,
            Description = input.Description.Trim(), PhotoUrl = Clean(input.PhotoUrl)
        };
        _db.Set<LostFoundSighting>().Add(s);
        if (r.ReporterId != uid)
            _db.Notifications.Add(new InAppNotification
            {
                UserId = r.ReporterId, Title = "New sighting reported on your post",
                Body = s.Description[..Math.Min(s.Description.Length, 120)], Url = $"/lost-found/{reportId}"
            });
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(s, uid, true, ct);
    }

    public async Task<IReadOnlyList<SightingDto>> ListSightingsAsync(Guid reportId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var r = await ReportAsync(reportId, ct);
        var all = CanSeeAll(r);
        var rows = await _db.Set<LostFoundSighting>().AsNoTracking().Where(s => s.ReportId == reportId
                && (all || s.Status == SightingStatus.Verified || s.ReporterUserId == uid))
            .OrderByDescending(s => s.SeenAt).Take(200).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(x => x.ReporterUserId), ct);
        return rows.Select(s => ToDto(s, names, uid, all)).ToList();
    }

    public async Task<SightingDto> ReviewSightingAsync(Guid reportId, Guid sightingId, bool verified, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await ReportAsync(reportId, ct);
        if (!CanSeeAll(r)) throw new ForbiddenException();
        var s = await _db.Set<LostFoundSighting>().SingleOrDefaultAsync(x => x.Id == sightingId && x.ReportId == reportId, ct)
            ?? throw new NotFoundException("Sighting", sightingId);
        if (s.Status != SightingStatus.Pending) throw new ConflictException("This sighting was already reviewed.");
        s.Status = verified ? SightingStatus.Verified : SightingStatus.Rejected;
        s.VerifiedByUserId = uid;
        s.VerifiedAt = DateTime.UtcNow;
        if (verified && s.ReporterUserId != uid)
            _db.Notifications.Add(new InAppNotification
            {
                UserId = s.ReporterUserId, Title = "Your sighting was verified", Body = "The owner confirmed your sighting. Thank you!",
                Url = $"/lost-found/{reportId}"
            });
        await _db.SaveChangesAsync(ct);
        return await ToDtoAsync(s, uid, true, ct);
    }

    private async Task<SightingDto> ToDtoAsync(LostFoundSighting s, Guid? uid, bool all, CancellationToken ct) =>
        ToDto(s, await NamesAsync(new[] { s.ReporterUserId }, ct), uid, all);

    private static SightingDto ToDto(LostFoundSighting s, Dictionary<Guid, string> names, Guid? uid, bool privileged)
    {
        var mine = s.ReporterUserId == uid;
        // Non-owners only ever see sighting coordinates they submitted themselves, fuzzed otherwise.
        (double? lat, double? lng) = privileged || mine ? (s.Latitude, s.Longitude) : LostFoundLogic.Fuzz(s.Id, s.Latitude, s.Longitude);
        return new SightingDto(s.Id, s.ReportId, s.ReporterUserId, names.GetValueOrDefault(s.ReporterUserId, ""), s.SeenAt, lat, lng,
            s.Description, s.PhotoUrl, s.Status, s.VerifiedAt, s.CreatedAt, mine);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    // ---------------------------------------------------------------- timeline

    public async Task<IReadOnlyList<TimelineEventDto>> TimelineAsync(Guid reportId, CancellationToken ct = default)
    {
        var r = await ReportAsync(reportId, ct);
        var all = CanSeeAll(r);
        var uid = _current.UserId;
        var ev = new List<TimelineEventDto>
        {
            new("Created", r.CreatedAt, r.Kind == LostFoundKind.Lost ? "Lost report posted" : "Found report posted", r.City, r.Id)
        };

        var sightings = await _db.Set<LostFoundSighting>().AsNoTracking().Where(s => s.ReportId == reportId
            && (all || s.Status == SightingStatus.Verified || s.ReporterUserId == uid)).ToListAsync(ct);
        foreach (var s in sightings)
        {
            ev.Add(new TimelineEventDto("Sighting", s.SeenAt, "Sighting reported",
                s.Description[..Math.Min(s.Description.Length, 140)], s.Id));
            if (s.VerifiedAt is { } at)
                ev.Add(new TimelineEventDto(s.Status == SightingStatus.Verified ? "SightingVerified" : "SightingRejected", at,
                    s.Status == SightingStatus.Verified ? "Sighting verified by owner" : "Sighting rejected by owner", null, s.Id));
        }

        // Match outreach is not stored separately: it is the notifications sent at creation time pointing at this report.
        var url = $"/lost-found/{reportId}";
        var contacted = await _db.Notifications.AsNoTracking()
            .Where(n => n.Url == url && n.UserId != r.ReporterId
                && (n.Title == "A pet matching your lost report was found" || n.Title == "Someone is looking for an animal you found"))
            .CountAsync(ct);
        if (contacted > 0)
            ev.Add(new TimelineEventDto("MatchesContacted", r.CreatedAt.AddSeconds(1), $"{contacted} possible match(es) contacted", null, null));

        if (all)
        {
            var evidence = await _db.Set<LostFoundEvidence>().AsNoTracking().Where(e => e.ReportId == reportId).ToListAsync(ct);
            foreach (var e in evidence)
                ev.Add(new TimelineEventDto("Evidence", e.CreatedAt, $"{e.Kind} evidence added", null, e.Id));
        }

        if (r.ResolvedAt is { } done)
            ev.Add(new TimelineEventDto("Resolved", done, r.Status == LostFoundStatus.Reunited ? "Reunited" : "Report closed", null, r.Id));

        return ev.OrderBy(e => e.At).ToList();
    }

    // ---------------------------------------------------------------- evidence

    public async Task<EvidenceDto> AddEvidenceAsync(Guid reportId, AddEvidenceInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await ReportAsync(reportId, ct);
        if (!CanSeeAll(r)) throw new ForbiddenException();
        if (!Uri.TryCreate(input.Url, UriKind.Absolute, out _) || input.Url.Length > 1000)
            throw Invalid("url", "Provide the uploaded file URL.");
        if (await _db.Set<LostFoundEvidence>().CountAsync(e => e.ReportId == reportId, ct) >= MaxEvidencePerReport)
            throw new ConflictException("Evidence limit reached for this report.");
        var e = new LostFoundEvidence { ReportId = reportId, Url = input.Url.Trim(), Kind = input.Kind, UploadedBy = uid };
        _db.Set<LostFoundEvidence>().Add(e);
        await _db.SaveChangesAsync(ct);
        return new EvidenceDto(e.Id, e.ReportId, e.Url, e.Kind, e.UploadedBy, e.CreatedAt);
    }

    public async Task<IReadOnlyList<EvidenceDto>> ListEvidenceAsync(Guid reportId, CancellationToken ct = default)
    {
        var r = await ReportAsync(reportId, ct);
        if (!CanSeeAll(r)) throw new ForbiddenException();
        return await _db.Set<LostFoundEvidence>().AsNoTracking().Where(e => e.ReportId == reportId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EvidenceDto(e.Id, e.ReportId, e.Url, e.Kind, e.UploadedBy, e.CreatedAt)).ToListAsync(ct);
    }

    // ---------------------------------------------------------------- alert subscriptions

    public async Task<IReadOnlyList<AlertSubscriptionDto>> ListSubscriptionsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.Set<LostFoundAlertSubscription>().AsNoTracking().Where(s => s.UserId == uid)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new AlertSubscriptionDto(s.Id, s.Latitude, s.Longitude, s.RadiusKm, s.AnimalType, s.Active, s.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AlertSubscriptionDto> SubscribeAsync(AlertSubscriptionInput i, CancellationToken ct = default)
    {
        var uid = Uid();
        if (double.IsNaN(i.Latitude) || double.IsNaN(i.Longitude) || !GeoSearch.IsValidPoint(i.Latitude, i.Longitude))
            throw Invalid("latitude", "Invalid coordinates.");
        if (!(i.RadiusKm >= 0.5 && i.RadiusKm <= 100)) throw Invalid("radiusKm", "Radius must be between 0.5 and 100 km.");
        if (await _db.Set<LostFoundAlertSubscription>().CountAsync(s => s.UserId == uid && s.Active, ct) >= MaxActiveSubscriptions)
            throw new ConflictException($"You can have up to {MaxActiveSubscriptions} alert areas.");
        var s = new LostFoundAlertSubscription
        {
            UserId = uid, Latitude = i.Latitude, Longitude = i.Longitude, RadiusKm = i.RadiusKm, AnimalType = i.AnimalType
        };
        _db.Set<LostFoundAlertSubscription>().Add(s);
        await _db.SaveChangesAsync(ct);
        return new AlertSubscriptionDto(s.Id, s.Latitude, s.Longitude, s.RadiusKm, s.AnimalType, s.Active, s.CreatedAt);
    }

    public async Task UnsubscribeAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        await _db.Set<LostFoundAlertSubscription>().Where(s => s.Id == id && s.UserId == uid).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Idempotent retention job body: nulls exact coordinates (city is kept) after the retention window.</summary>
public static class LostFoundLocationExpiry
{
    /// <summary>LostFound:ExactLocationDays (default 14; 0 disables expiry).</summary>
    public static int ConfiguredDays(Microsoft.Extensions.Configuration.IConfiguration c) =>
        int.TryParse(c["LostFound:ExactLocationDays"], out var d) && d >= 0 ? d : 14;

    public static async Task<int> RunAsync(ApplicationDbContext db, int days, DateTime nowUtc, CancellationToken ct = default)
    {
        if (days <= 0) return 0;
        var cutoff = nowUtc.AddDays(-days);
        return await db.LostFoundReports.IgnoreQueryFilters()
            .Where(r => r.CreatedAt <= cutoff && (r.Latitude != null || r.Longitude != null))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Latitude, (double?)null).SetProperty(r => r.Longitude, (double?)null), ct);
    }
}
