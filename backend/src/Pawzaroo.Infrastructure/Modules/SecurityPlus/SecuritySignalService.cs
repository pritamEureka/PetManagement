using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

public record LoginGeo(string? Ip, string? City, string? Country, double? Lat, double? Lon, DateTime At);

/// <summary>Pure detection rules (unit-testable).</summary>
public static class SignalRules
{
    public const double MaxSpeedKmh = 900;
    public static readonly TimeSpan CountryChangeWindow = TimeSpan.FromHours(1);

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Returns a human-readable reason when the move prev -> cur is implausible; otherwise null.</summary>
    public static string? EvaluateTravel(LoginGeo prev, LoginGeo cur)
    {
        var elapsed = cur.At - prev.At;
        if (elapsed <= TimeSpan.Zero) elapsed = TimeSpan.FromSeconds(1);

        if (prev.Lat is { } la1 && prev.Lon is { } lo1 && cur.Lat is { } la2 && cur.Lon is { } lo2)
        {
            var km = HaversineKm(la1, lo1, la2, lo2);
            var kmh = km / elapsed.TotalHours;
            if (km > 50 && kmh > MaxSpeedKmh) return $"Implied speed {kmh:F0} km/h over {km:F0} km.";
            return null;
        }

        if (!string.IsNullOrWhiteSpace(prev.Country) && !string.IsNullOrWhiteSpace(cur.Country) &&
            !string.Equals(prev.Country, cur.Country, StringComparison.OrdinalIgnoreCase) &&
            elapsed < CountryChangeWindow)
            return $"Country changed {prev.Country} -> {cur.Country} within {elapsed.TotalMinutes:F0} min.";
        return null;
    }

    public static bool ThresholdCrossed(long count, int threshold) => count == threshold;
}

public class SecuritySignalService : ISecuritySignalService
{
    private readonly ApplicationDbContext _db;
    private readonly CacheHelper _cache;
    private readonly IDegradedModePolicy _policy;
    private readonly IHttpContextAccessor _http;
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly ILogger<SecuritySignalService> _log;

    private readonly IIpGeoLookup? _geo;

    public SecuritySignalService(ApplicationDbContext db, CacheHelper cache, IDegradedModePolicy policy,
        IHttpContextAccessor http, IServiceProvider sp, IConfiguration cfg, ILogger<SecuritySignalService> log,
        IIpGeoLookup? geo = null)
    {
        _db = db; _cache = cache; _policy = policy; _http = http; _sp = sp; _cfg = cfg; _log = log; _geo = geo;
    }

    private int Cfg(string key, int def) => int.TryParse(_cfg[$"Security:Signals:{key}"], out var v) ? v : def;
    private string? CallerIp() => _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s.Trim().ToLowerInvariant())))[..24];

    private async Task WriteSignalAsync(Guid? userId, string? ip, string type, string severity, object details, CancellationToken ct)
    {
        _db.Set<SecuritySignal>().Add(new SecuritySignal
        {
            UserId = userId, Ip = ip, Type = type, Severity = severity,
            DetailsJson = JsonSerializer.Serialize(details)
        });
        await _db.SaveChangesAsync(ct);
        _log.LogWarning("Security signal {Type} severity={Severity} user={UserId} ip={Ip}", type, severity, userId, ip);
    }

    // ---- account enumeration ------------------------------------------------

    public async Task<bool> RecordIdentifierAttemptAsync(string? ip, string identifier, string surface, CancellationToken ct = default)
    {
        ip ??= CallerIp();
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(identifier)) return false;
        var threshold = Cfg("EnumerationDistinctEmails", 10);
        var window = TimeSpan.FromMinutes(Cfg("EnumerationWindowMinutes", 15));
        var setKey = $"sec:enum:{ip}";
        var lockKey = $"sec:enum:lock:{ip}";
        try
        {
            var db = _cache.Db;
            if (await db.KeyExistsAsync(lockKey)) return true;
            await db.SetAddAsync(setKey, Hash(identifier));
            var n = await db.SetLengthAsync(setKey);
            if (n == 1) await db.KeyExpireAsync(setKey, window);
            if (n < threshold) return false;

            if (await db.StringSetAsync(lockKey, "1", window, When.NotExists))
                await WriteSignalAsync(null, ip, "account_enumeration", "high",
                    new { surface, distinctIdentifiers = n, windowMinutes = window.TotalMinutes }, ct);
            return true;
        }
        catch (RedisException ex)
        {
            _policy.Report(DegradedSites.SecuritySignals, ex);
            return _policy.For(DegradedSites.SecuritySignals) == DegradedModePolicy.FailClosed;
        }
    }

    // ---- impossible travel --------------------------------------------------

    public async Task AnalyzeLoginAsync(Guid userId, string? ip, CancellationToken ct = default)
    {
        var device = await _db.UserDevices.AsNoTracking()
            .Where(d => d.UserId == userId).OrderByDescending(d => d.LastSeenAt).FirstOrDefaultAsync(ct);
        var header = _cfg["Security:Geo:CountryHeader"] ?? "CF-IPCountry";
        // Pluggable geo source first (edge-proxy headers or a GeoIP DB); falls back to the legacy country-only behaviour.
        var geo = _geo?.Lookup(ip);
        var country = geo?.Country ?? device?.IpCountry ?? _http.HttpContext?.Request.Headers[header].ToString();
        if (string.IsNullOrWhiteSpace(country)) country = null;

        // Lat/Lon exist only with a geo source; without one just the country-change rule can fire.
        var cur = new LoginGeo(ip, geo?.City ?? device?.IpCity, country, geo?.Lat, geo?.Lon, DateTime.UtcNow);
        var key = $"sec:lastlogin:{userId}";
        var prev = await _cache.GetAsync<LoginGeo>(key);
        await _cache.SetAsync(key, cur, TimeSpan.FromDays(2));
        if (prev is null) return;

        var reason = SignalRules.EvaluateTravel(prev, cur);
        if (reason is null) return;

        await WriteSignalAsync(userId, ip, "impossible_travel", "high",
            new { reason, from = new { prev.Ip, prev.City, prev.Country, prev.At }, to = new { cur.Ip, cur.City, cur.Country, cur.At } }, ct);
        await NotifyAsync(userId, "Unusual sign-in location",
            "We noticed a sign-in from a location far from your previous one. If this wasn't you, change your password and sign out all sessions.", ct);
    }

    // ---- OTP failures -------------------------------------------------------

    public async Task<bool> IsOtpLockedAsync(Guid userId, CancellationToken ct = default)
    {
        try { return await _cache.Db.KeyExistsAsync($"sec:otp:lock:{userId}"); }
        catch (RedisException ex)
        {
            _policy.Report(DegradedSites.OtpCounters, ex);
            return _policy.For(DegradedSites.OtpCounters) == DegradedModePolicy.FailClosed;
        }
    }

    public async Task<bool> RecordOtpFailureAsync(Guid userId, string? ip, CancellationToken ct = default)
    {
        ip ??= CallerIp();
        var threshold = Cfg("OtpFailureThreshold", 10);
        var window = TimeSpan.FromMinutes(Cfg("OtpFailureWindowMinutes", 15));
        var lockFor = TimeSpan.FromMinutes(Cfg("OtpLockMinutes", 30));
        try
        {
            var db = _cache.Db;
            var uKey = $"sec:otp:fail:u:{userId}";
            var un = await db.StringIncrementAsync(uKey);
            if (un == 1) await db.KeyExpireAsync(uKey, window);

            long inCount = 0;
            if (!string.IsNullOrWhiteSpace(ip))
            {
                var iKey = $"sec:otp:fail:ip:{ip}";
                inCount = await db.StringIncrementAsync(iKey);
                if (inCount == 1) await db.KeyExpireAsync(iKey, window);
            }

            if (un < threshold && inCount < threshold * 3) return false;
            if (!await db.StringSetAsync($"sec:otp:lock:{userId}", "1", lockFor, When.NotExists)) return true;

            await WriteSignalAsync(userId, ip, "otp_failures", "high",
                new { userFailures = un, ipFailures = inCount, lockMinutes = lockFor.TotalMinutes }, ct);
            await NotifyAsync(userId, "Verification locked",
                "Too many incorrect verification codes were entered, so code verification is temporarily locked.", ct);
            return true;
        }
        catch (RedisException ex)
        {
            _policy.Report(DegradedSites.OtpCounters, ex);
            return _policy.For(DegradedSites.OtpCounters) == DegradedModePolicy.FailClosed;
        }
    }

    private async Task NotifyAsync(Guid userId, string title, string body, CancellationToken ct)
    {
        try
        {
            var n = _sp.GetService<INotificationService>();
            if (n is not null) await n.NotifyUserAsync(userId, title, body, new { type = "security.signal" }, ct);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Security notification failed for {UserId}", userId); }
    }

    // ---- admin list ---------------------------------------------------------

    public async Task<IReadOnlyList<SecuritySignalDto>> ListAsync(string? type, string? severity, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.Set<SecuritySignal>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) q = q.Where(s => s.Type == type);
        if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(s => s.Severity == severity);
        return await q.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SecuritySignalDto(s.Id, s.UserId, s.Ip, s.Type, s.Severity, s.DetailsJson, s.CreatedAt))
            .ToListAsync(ct);
    }
}
