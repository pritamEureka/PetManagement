using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Events;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class LostFoundService : ILostFoundService
{
    /// <summary>Cap on "nearby" fan-out per report so one alert can't page the whole city.</summary>
    private const int MaxNeighbourAlerts = 200;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFeedService _feed;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public LostFoundService(ApplicationDbContext db, ICurrentUserService current, IFeedService feed,
        IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _feed = feed;
        _kafka = kafka;
        _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool IsModerator => _current.Permissions.Contains(Permissions.LostFound.Moderate);

    public async Task<PagedLostFound> SearchAsync(LostFoundQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 50);
        var query = _db.LostFoundReports.AsNoTracking().Where(r => r.Status == (q.Status ?? LostFoundStatus.Open));
        if (q.Kind is { } k) query = query.Where(r => r.Kind == k);
        if (q.AnimalType is { } t) query = query.Where(r => r.AnimalType == t);
        if (!string.IsNullOrWhiteSpace(q.City))
        {
            var city = $"%{q.City.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.City, city));
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = $"%{q.Q.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Description, term)
                || (r.Breed != null && EF.Functions.ILike(r.Breed, term))
                || (r.Color != null && EF.Functions.ILike(r.Color, term))
                || (r.Area != null && EF.Functions.ILike(r.Area, term)));
        }

        var total = await query.CountAsync(ct);
        var items = await Project(query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedLostFound(items, total, page, size);
    }

    public async Task<IReadOnlyList<LostFoundSummaryDto>> MineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await Project(_db.LostFoundReports.AsNoTracking().Where(r => r.ReporterId == uid)
            .OrderByDescending(r => r.CreatedAt)).ToListAsync(ct);
    }

    public async Task<LostFoundDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var r = await _db.LostFoundReports.AsNoTracking()
            .Include(x => x.Reporter).Include(x => x.Pet)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Report", id);
        return new LostFoundDetailDto(ToSummary(r), r.Latitude, r.Longitude, r.ContactPreference,
            r.ContactPreference == Domain.Common.ContactPreference.Phone ? r.ContactPhone : null,
            r.Pet?.MicrochipNumber, r.Pet?.TagNumber, r.ResolvedAt, r.FeedPostId, r.ReporterId == uid);
    }

    public async Task<LostFoundDetailDto> CreateAsync(CreateLostFoundInput i, CancellationToken ct = default)
    {
        var uid = Uid();
        if (await _db.LostFoundReports.CountAsync(r => r.ReporterId == uid && r.Status == LostFoundStatus.Open, ct) >= 10)
            throw new ConflictException("You already have 10 open reports. Resolve some before posting more.");

        var report = new LostFoundReport
        {
            ReporterId = uid, Kind = i.Kind, Description = i.Description.Trim(),
            Breed = Clean(i.Breed), Color = Clean(i.Color), PhotoUrl = Clean(i.PhotoUrl),
            LastSeenAt = Utc(i.LastSeenAt), City = i.City.Trim(), Area = Clean(i.Area),
            Latitude = i.Latitude, Longitude = i.Longitude, ContactPreference = i.ContactPreference,
            ContactPhone = Clean(i.ContactPhone)
        };

        Pet? pet = null;
        if (i.PetId is { } pid)
        {
            if (i.Kind != LostFoundKind.Lost) throw new ConflictException("Only lost reports can reference your pet.");
            pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == pid && p.OwnerId == uid, ct)
                ?? throw new NotFoundException("Pet", pid);
            if (await _db.LostFoundReports.AnyAsync(r => r.PetId == pid && r.Status == LostFoundStatus.Open, ct))
                throw new ConflictException($"{pet.Name} already has an open lost report.");
            report.PetId = pid;
            report.AnimalType = pet.AnimalType;
            report.Breed ??= pet.Breed;
            report.Color ??= pet.Color;
            report.PhotoUrl ??= pet.PrimaryPhotoUrl;
            pet.IsLost = true;
        }
        else
        {
            report.AnimalType = i.AnimalType!.Value;
        }

        _db.LostFoundReports.Add(report);
        await _db.SaveChangesAsync(ct);

        if (i.ShareToFeed)
        {
            var heading = report.Kind == LostFoundKind.Lost
                ? $"🚨 LOST {report.AnimalType.ToString().ToUpperInvariant()}{(pet is null ? "" : $": {pet.Name}")}"
                : $"🐾 FOUND {report.AnimalType.ToString().ToUpperInvariant()}";
            var where = report.Area is null ? report.City : $"{report.Area}, {report.City}";
            var content = $"{heading}\nLast seen {report.LastSeenAt:dd MMM, HH:mm} UTC near {where}.\n" +
                          $"{(report.Breed is null ? "" : $"Breed: {report.Breed}. ")}{(report.Color is null ? "" : $"Colour: {report.Color}. ")}\n" +
                          $"{report.Description}\n\nSee details: /lost-found/{report.Id}";
            var postId = await _feed.CreateAsync(new CreatePostInput(content, report.AnimalType.ToString(), report.City,
                report.PhotoUrl is null ? null : new[] { report.PhotoUrl },
                new[] { report.Kind == LostFoundKind.Lost ? "lostpet" : "foundpet", report.City.Replace(" ", "").ToLowerInvariant() },
                pet is null ? null : new[] { pet.Id }), ct);
            report.FeedPostId = postId;
        }

        await NotifyNeighboursAsync(report, pet?.Name, ct);
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync<object>(PetTopics.Events, report.Kind == LostFoundKind.Lost
            ? new PetReportedLost(report.Id, report.PetId, uid, report.City, DateTime.UtcNow)
            : new PetFound(report.Id, uid, report.City, DateTime.UtcNow), report.Id.ToString(), ct);

        return await GetAsync(report.Id, ct);
    }

    private async Task NotifyNeighboursAsync(LostFoundReport r, string? petName, CancellationToken ct)
    {
        // City match on the free-text profile location. A geo-radius query can
        // replace this once user coordinates are captured.
        var city = $"%{r.City}%";
        var neighbours = await _db.Users.AsNoTracking()
            .Where(u => u.Id != r.ReporterId && u.IsActive && !u.IsSuspended && u.Location != null && EF.Functions.ILike(u.Location, city))
            .OrderByDescending(u => u.LastLoginAt)
            .Select(u => u.Id).Take(MaxNeighbourAlerts).ToListAsync(ct);

        var title = r.Kind == LostFoundKind.Lost
            ? $"Lost {r.AnimalType.ToString().ToLowerInvariant()} near you{(petName is null ? "" : $": {petName}")}"
            : $"A {r.AnimalType.ToString().ToLowerInvariant()} was found near you";
        var body = $"{(r.Area is null ? r.City : $"{r.Area}, {r.City}")} · {r.Description[..Math.Min(r.Description.Length, 120)]}";
        foreach (var id in neighbours)
            _db.Notifications.Add(new InAppNotification { UserId = id, Title = title, Body = body, Url = $"/lost-found/{r.Id}" });

        // Pre-emptively tell reporters of likely matches.
        var matches = await FindMatchesAsync(r, ct);
        foreach (var m in matches.Where(m => m.Score >= 50).Take(10))
        {
            _db.Notifications.Add(new InAppNotification
            {
                UserId = m.Report.ReporterId,
                Title = r.Kind == LostFoundKind.Found ? "A pet matching your lost report was found" : "Someone is looking for an animal you found",
                Body = $"{string.Join(", ", m.Reasons)} — have a look.",
                Url = $"/lost-found/{r.Id}"
            });
        }
    }

    public async Task ResolveAsync(Guid id, ResolveLostFoundInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await _db.LostFoundReports.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Report", id);
        if (r.ReporterId != uid && !IsModerator) throw new ForbiddenException();
        if (r.Status != LostFoundStatus.Open) throw new ConflictException("This report is already closed.");

        r.Status = input.Reunited ? LostFoundStatus.Reunited : LostFoundStatus.Closed;
        r.ResolvedAt = DateTime.UtcNow;
        if (r.PetId is { } pid)
        {
            var pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == pid, ct);
            if (pet is not null) pet.IsLost = false;
        }
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events, new LostFoundResolved(id, input.Reunited, DateTime.UtcNow), id.ToString(), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var r = await _db.LostFoundReports.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Report", id);
        if (r.ReporterId != uid && !IsModerator) throw new ForbiddenException();
        if (r.PetId is { } pid && r.Status == LostFoundStatus.Open)
        {
            var pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == pid, ct);
            if (pet is not null) pet.IsLost = false;
        }
        _db.LostFoundReports.Remove(r);
        await _db.SaveChangesAsync(ct);
        if (r.ReporterId != uid)
            await _audit.LogAsync("moderate_delete", "LostFoundReport", id.ToString(), "LostFound", ct: ct);
    }

    public async Task<IReadOnlyList<LostFoundMatchDto>> MatchesAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.LostFoundReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Report", id);
        return await FindMatchesAsync(r, ct);
    }

    private async Task<IReadOnlyList<LostFoundMatchDto>> FindMatchesAsync(LostFoundReport r, CancellationToken ct)
    {
        var opposite = r.Kind == LostFoundKind.Lost ? LostFoundKind.Found : LostFoundKind.Lost;
        var windowStart = r.LastSeenAt.AddDays(-30);
        var windowEnd = r.LastSeenAt.AddDays(30);
        var candidates = await _db.LostFoundReports.AsNoTracking()
            .Include(x => x.Reporter).Include(x => x.Pet)
            .Where(x => x.Id != r.Id && x.Kind == opposite && x.Status == LostFoundStatus.Open
                     && x.AnimalType == r.AnimalType && x.LastSeenAt >= windowStart && x.LastSeenAt <= windowEnd)
            .OrderByDescending(x => x.CreatedAt).Take(200)
            .ToListAsync(ct);

        static bool Similar(string? a, string? b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase));

        var results = new List<LostFoundMatchDto>();
        foreach (var c in candidates)
        {
            var score = 20;   // same species
            var reasons = new List<string> { $"Same species ({c.AnimalType})" };
            if (string.Equals(c.City, r.City, StringComparison.OrdinalIgnoreCase)) { score += 30; reasons.Add($"Same city ({c.City})"); }
            if (Similar(c.Area, r.Area)) { score += 15; reasons.Add("Same area"); }
            if (Similar(c.Color, r.Color)) { score += 20; reasons.Add($"Colour: {c.Color}"); }
            if (Similar(c.Breed, r.Breed)) { score += 15; reasons.Add($"Breed: {c.Breed}"); }
            if (Math.Abs((c.LastSeenAt - r.LastSeenAt).TotalDays) <= 3) { score += 10; reasons.Add("Seen within 3 days"); }
            if (r.Latitude is { } la && r.Longitude is { } lo && c.Latitude is { } lb && c.Longitude is { } lob
                && DistanceKm(la, lo, lb, lob) <= 5) { score += 20; reasons.Add("Within 5 km"); }
            if (score >= 40) results.Add(new LostFoundMatchDto(ToSummary(c), Math.Min(score, 100), reasons));
        }
        return results.OrderByDescending(m => m.Score).Take(20).ToList();
    }

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private static IQueryable<LostFoundSummaryDto> Project(IQueryable<LostFoundReport> q) =>
        q.Select(r => new LostFoundSummaryDto(r.Id, r.Kind, r.Status, r.AnimalType, r.Breed, r.Color, r.Description,
            r.PhotoUrl, r.LastSeenAt, r.City, r.Area, r.ReporterId, r.Reporter.DisplayName, r.PetId,
            r.Pet != null ? r.Pet.Name : null, r.CreatedAt));

    private static LostFoundSummaryDto ToSummary(LostFoundReport r) =>
        new(r.Id, r.Kind, r.Status, r.AnimalType, r.Breed, r.Color, r.Description, r.PhotoUrl, r.LastSeenAt, r.City,
            r.Area, r.ReporterId, r.Reporter?.DisplayName ?? "", r.PetId, r.Pet?.Name, r.CreatedAt);
}
