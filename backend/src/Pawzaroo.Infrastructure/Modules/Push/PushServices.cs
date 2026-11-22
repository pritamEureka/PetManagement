using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Push;

public class PushSubscriptionService : IPushSubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public PushSubscriptionService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<PushSubscriptionDto> SubscribeAsync(PushSubscribeRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var endpoint = req.Endpoint?.Trim();
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2048
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw Invalid("endpoint", "A valid https push endpoint is required.");
        if (string.IsNullOrWhiteSpace(req.P256dh) || req.P256dh.Length > 256) throw Invalid("p256dh", "Invalid key.");
        if (string.IsNullOrWhiteSpace(req.Auth) || req.Auth.Length > 128) throw Invalid("auth", "Invalid key.");
        var ua = req.UserAgent is { Length: > 400 } ? req.UserAgent[..400] : req.UserAgent;

        // Endpoints are globally unique; a re-subscribe (even by another user on a shared browser) re-owns it.
        var sub = await _db.Set<PushSubscription>().IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (sub is null)
        {
            sub = new PushSubscription { Endpoint = endpoint };
            _db.Set<PushSubscription>().Add(sub);
        }
        sub.UserId = uid;
        sub.P256dh = req.P256dh;
        sub.Auth = req.Auth;
        sub.UserAgent = ua;
        sub.IsDeleted = false;
        sub.DeletedAt = null;
        await _db.SaveChangesAsync(ct);
        return new PushSubscriptionDto(sub.Id, sub.Endpoint, sub.UserAgent, sub.CreatedAt);
    }

    public async Task<bool> UnsubscribeAsync(string endpoint, CancellationToken ct = default)
    {
        var uid = Uid();
        // Hard delete so the unique endpoint index can be reused.
        var n = await _db.Set<PushSubscription>().Where(s => s.UserId == uid && s.Endpoint == endpoint).ExecuteDeleteAsync(ct);
        return n > 0;
    }

    public async Task<IReadOnlyList<PushSubscriptionDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.Set<PushSubscription>().AsNoTracking().Where(s => s.UserId == uid)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new PushSubscriptionDto(s.Id, s.Endpoint, s.UserAgent, s.CreatedAt))
            .ToListAsync(ct);
    }

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });
}

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public NotificationPreferenceService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<NotificationPreferencesDto> GetAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<NotificationPreference>().AsNoTracking().Where(p => p.UserId == uid).ToListAsync(ct);
        var quiet = await _db.Set<NotificationQuietHours>().AsNoTracking().FirstOrDefaultAsync(q => q.UserId == uid, ct);
        return Build(rows, quiet);
    }

    public async Task<NotificationPreferencesDto> PutAsync(NotificationPreferencesDto dto, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = dto.QuietHours;
        if (q.StartMinute is < 0 or > 1439 || q.EndMinute is < 0 or > 1439)
            throw Invalid("quietHours", "Start/end must be minutes of day (0-1439).");
        if (q.TimezoneOffsetMinutes is < -840 or > 840)
            throw Invalid("quietHours", "Invalid timezone offset.");
        foreach (var c in dto.Categories ?? Array.Empty<CategoryPreferenceDto>())
            if (!NotificationCategories.All.Contains(c.Category))
                throw Invalid("categories", $"Unknown category '{c.Category}'.");

        var rows = await _db.Set<NotificationPreference>().Where(p => p.UserId == uid).ToListAsync(ct);
        foreach (var c in dto.Categories ?? Array.Empty<CategoryPreferenceDto>())
        {
            var row = rows.FirstOrDefault(r => r.Category == c.Category);
            if (row is null)
            {
                row = new NotificationPreference { UserId = uid, Category = c.Category };
                _db.Set<NotificationPreference>().Add(row);
                rows.Add(row);
            }
            row.InAppEnabled = c.InApp;
            row.PushEnabled = c.Push;
            row.EmailEnabled = c.Email;
        }

        var quiet = await _db.Set<NotificationQuietHours>().FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (quiet is null)
        {
            quiet = new NotificationQuietHours { UserId = uid };
            _db.Set<NotificationQuietHours>().Add(quiet);
        }
        quiet.Enabled = q.Enabled;
        quiet.StartMinute = q.StartMinute;
        quiet.EndMinute = q.EndMinute;
        quiet.TimezoneOffsetMinutes = q.TimezoneOffsetMinutes;

        await _db.SaveChangesAsync(ct);
        return Build(rows, quiet);
    }

    private static NotificationPreferencesDto Build(IEnumerable<NotificationPreference> rows, NotificationQuietHours? quiet)
    {
        var map = rows.ToDictionary(r => r.Category);
        var cats = NotificationCategories.All.Select(c => map.TryGetValue(c, out var r)
            ? new CategoryPreferenceDto(c, r.InAppEnabled, r.PushEnabled, r.EmailEnabled)
            : new CategoryPreferenceDto(c, true, true, true)).ToList();
        var q = quiet is null
            ? new QuietHoursDto(false, 22 * 60, 7 * 60, 0)
            : new QuietHoursDto(quiet.Enabled, quiet.StartMinute, quiet.EndMinute, quiet.TimezoneOffsetMinutes);
        return new NotificationPreferencesDto(cats, q);
    }

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });
}

/// <summary>
/// Default <see cref="IWebPushSender"/>: logs instead of calling the push service, so the feature is
/// safe to run without VAPID keys or a Web Push library (no extra NuGet packages are referenced).
/// To go live, implement IWebPushSender (VAPID JWT signing + aes128gcm payload encryption, POST to
/// the endpoint with TTL header) and register it AFTER AddCommunityPlusFeatures so it replaces this one.
/// Configure Push:VapidPublicKey / Push:VapidPrivateKey / Push:Subject for that implementation.
/// </summary>
public class LoggingWebPushSender : IWebPushSender
{
    private readonly ILogger<LoggingWebPushSender> _log;
    public LoggingWebPushSender(ILogger<LoggingWebPushSender> log) => _log = log;

    public Task<WebPushResult> SendAsync(string endpoint, string p256dh, string auth, PushMessage message, CancellationToken ct = default)
    {
        var host = Uri.TryCreate(endpoint, UriKind.Absolute, out var u) ? u.Host : "unknown";
        _log.LogInformation("[web-push:log-only] would send '{Title}' (category {Category}) to {Host}",
            message.Title, message.Category, host);
        return Task.FromResult(WebPushResult.Sent);
    }
}

public class PushDispatcher : IPushDispatcher
{
    private readonly ApplicationDbContext _db;
    private readonly IWebPushSender _sender;

    public PushDispatcher(ApplicationDbContext db, IWebPushSender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task<bool> ShouldDeliverAsync(Guid userId, string category, DateTime now, CancellationToken ct = default)
    {
        var pref = await _db.Set<NotificationPreference>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Category == category, ct);
        var quiet = await _db.Set<NotificationQuietHours>().AsNoTracking().FirstOrDefaultAsync(q => q.UserId == userId, ct);
        // Category-level mute (scope Category). Conversation/community/source mutes are enforced by
        // INotificationMuteService.IsMutedAsync, which producers that know those ids should call.
        var categoryMuted = await _db.Set<NotificationMute>().AsNoTracking().AnyAsync(m =>
            m.UserId == userId && m.Scope == NotificationMuteScope.Category && m.ScopeId == category
            && (m.MutedUntil == null || m.MutedUntil > now), ct);
        if (categoryMuted) return false;
        return NotificationPolicy.ShouldDeliver(
            NotificationChannel.Push,
            pref is null ? null : new ChannelPrefs(pref.InAppEnabled, pref.PushEnabled, pref.EmailEnabled),
            quiet is null ? null : new QuietHoursSetting(quiet.Enabled, quiet.StartMinute, quiet.EndMinute, quiet.TimezoneOffsetMinutes),
            now);
    }

    public async Task<int> DispatchAsync(Guid userId, PushMessage message, CancellationToken ct = default)
    {
        if (!await ShouldDeliverAsync(userId, message.Category ?? NotificationCategories.System, DateTime.UtcNow, ct)) return 0;
        var subs = await _db.Set<PushSubscription>().Where(s => s.UserId == userId).ToListAsync(ct);
        var sent = 0;
        var gone = new List<Guid>();
        foreach (var s in subs)
        {
            var r = await _sender.SendAsync(s.Endpoint, s.P256dh, s.Auth, message, ct);
            if (r == WebPushResult.Sent) sent++;
            else if (r == WebPushResult.Gone) gone.Add(s.Id);
        }
        if (gone.Count > 0)
            await _db.Set<PushSubscription>().Where(s => gone.Contains(s.Id)).ExecuteDeleteAsync(ct);
        return sent;
    }
}
