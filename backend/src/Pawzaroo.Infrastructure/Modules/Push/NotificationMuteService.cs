using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Push;

public class NotificationMuteService : INotificationMuteService
{
    private const int MaxMutesPerUser = 200;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public NotificationMuteService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });

    private static NotificationMuteDto ToDto(NotificationMute m) =>
        new(m.Id, m.Scope.ToString(), m.ScopeId, m.MutedUntil, m.CreatedAt);

    public async Task<IReadOnlyList<NotificationMuteDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var rows = await _db.Set<NotificationMute>().AsNoTracking()
            .Where(m => m.UserId == uid && (m.MutedUntil == null || m.MutedUntil > now))
            .OrderByDescending(m => m.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<NotificationMuteDto> MuteAsync(CreateNotificationMuteRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        if (!Enum.TryParse<NotificationMuteScope>(req.Scope, true, out var scope))
            throw Invalid("scope", "Unknown mute scope.");
        var scopeId = req.ScopeId?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(scopeId) || scopeId.Length > 100) throw Invalid("scopeId", "A scope id is required.");
        if (scope == NotificationMuteScope.Category && !NotificationCategories.All.Contains(scopeId))
            throw Invalid("scopeId", $"Unknown category '{scopeId}'.");
        if (req.DurationHours is < 1 or > 24 * 365) throw Invalid("durationHours", "Duration must be 1 to 8760 hours.");

        var until = req.DurationHours is { } h ? DateTime.UtcNow.AddHours(h) : (DateTime?)null;

        var existing = await _db.Set<NotificationMute>()
            .FirstOrDefaultAsync(m => m.UserId == uid && m.Scope == scope && m.ScopeId == scopeId, ct);
        if (existing is null)
        {
            if (await _db.Set<NotificationMute>().CountAsync(m => m.UserId == uid, ct) >= MaxMutesPerUser)
                throw new AppException("rate_limited", "Too many muted sources. Unmute some first.");
            existing = new NotificationMute { UserId = uid, Scope = scope, ScopeId = scopeId };
            _db.Set<NotificationMute>().Add(existing);
        }
        existing.MutedUntil = until;
        await _db.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    public async Task<bool> UnmuteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var row = await _db.Set<NotificationMute>().FirstOrDefaultAsync(m => m.Id == id && m.UserId == uid, ct);
        if (row is null) return false;
        _db.Set<NotificationMute>().Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DigestPreferenceDto>> GetDigestAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<NotificationDigestPreference>().AsNoTracking().Where(p => p.UserId == uid).ToListAsync(ct);
        var map = rows.ToDictionary(r => r.Category, r => r.Frequency);
        return NotificationCategories.All
            .Select(c => new DigestPreferenceDto(c, (map.TryGetValue(c, out var f) ? f : NotificationDigestFrequency.Instant).ToString()))
            .ToList();
    }

    public async Task<IReadOnlyList<DigestPreferenceDto>> PutDigestAsync(IReadOnlyList<DigestPreferenceDto> prefs, CancellationToken ct = default)
    {
        var uid = Uid();
        foreach (var p in prefs)
        {
            if (!NotificationCategories.All.Contains(p.Category)) throw Invalid("category", $"Unknown category '{p.Category}'.");
            if (!Enum.TryParse<NotificationDigestFrequency>(p.Frequency, true, out _)) throw Invalid("frequency", "Frequency must be Instant, Hourly or Daily.");
        }
        var rows = await _db.Set<NotificationDigestPreference>().Where(r => r.UserId == uid).ToListAsync(ct);
        foreach (var p in prefs)
        {
            var freq = Enum.Parse<NotificationDigestFrequency>(p.Frequency, true);
            var row = rows.FirstOrDefault(r => r.Category == p.Category);
            if (row is null)
            {
                row = new NotificationDigestPreference { UserId = uid, Category = p.Category };
                _db.Set<NotificationDigestPreference>().Add(row);
                rows.Add(row);
            }
            row.Frequency = freq;
        }
        await _db.SaveChangesAsync(ct);
        return await GetDigestAsync(ct);
    }

    public async Task<bool> IsMutedAsync(Guid userId, string? category, IEnumerable<string>? sourceKeys, DateTime nowUtc, CancellationToken ct = default)
    {
        var keys = (sourceKeys ?? Array.Empty<string>()).Select(k => k.ToLowerInvariant()).ToHashSet();
        if (category is null && keys.Count == 0) return false;
        var mutes = await _db.Set<NotificationMute>().AsNoTracking()
            .Where(m => m.UserId == userId && (m.MutedUntil == null || m.MutedUntil > nowUtc))
            .Select(m => new { m.Scope, m.ScopeId }).ToListAsync(ct);
        return mutes.Any(m => m.Scope == NotificationMuteScope.Category
            ? category is not null && m.ScopeId == category
            : keys.Contains($"{m.Scope.ToString().ToLowerInvariant()}:{m.ScopeId}"));
    }
}
