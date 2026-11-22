using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>What to do for a use-site when Redis is unavailable.</summary>
public enum DegradedModePolicy { FailOpen, FailClosed }

/// <summary>Named use-sites resolved by <see cref="IDegradedModePolicy"/>.</summary>
public static class DegradedSites
{
    public const string RateLimit = "RateLimit";                 // generic limiter; auth scopes handled specially
    public const string OtpCounters = "OtpCounters";             // OTP / login brute-force counters
    public const string PermissionCache = "PermissionCache";     // always falls back to DB
    public const string NotificationRateLimit = "NotificationRateLimit";
    public const string TokenBlacklist = "TokenBlacklist";
    public const string SecuritySignals = "SecuritySignals";   // detection counters (enumeration); fail open + report
}

public interface IDegradedModePolicy
{
    /// <summary>Policy for a use-site (see <see cref="DegradedSites"/>).</summary>
    DegradedModePolicy For(string site);

    /// <summary>Policy for a rate-limit scope; auth-sensitive scopes fail closed by default.</summary>
    DegradedModePolicy ForRateLimitScope(string scope);

    /// <summary>Logs a warning and bumps the degraded counter.</summary>
    void Report(string site, Exception ex);
}

/// <summary>
/// Config: <c>Security:RedisDegraded:{Site}</c> = FailOpen | FailClosed. For <c>RateLimit</c> also
/// <c>FailClosedForAuth</c> (default): auth scopes fail closed, everything else fails open.
/// </summary>
public class DegradedModePolicyService : IDegradedModePolicy
{
    private static readonly Meter Meter = new("Pawzaroo.Redis");
    private static readonly Counter<long> Degraded = Meter.CreateCounter<long>("pawzaroo.redis.degraded");
    private static readonly string[] AuthPrefixes = { "otp", "login", "auth", "recovery", "register", "passkey", "stepup", "password" };

    private readonly IConfiguration? _cfg;
    private readonly ILogger<DegradedModePolicyService>? _log;

    public DegradedModePolicyService(IConfiguration? cfg = null, ILogger<DegradedModePolicyService>? log = null)
    {
        _cfg = cfg;
        _log = log;
    }

    public static bool IsAuthScope(string scope)
        => AuthPrefixes.Any(p => scope.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pure resolution of a configured value; exposed for tests.</summary>
    public static DegradedModePolicy Resolve(string site, string? configured, string? scope = null)
    {
        if (site == DegradedSites.RateLimit)
        {
            var v = configured ?? "FailClosedForAuth";
            if (v.Equals("FailOpen", StringComparison.OrdinalIgnoreCase)) return DegradedModePolicy.FailOpen;
            if (v.Equals("FailClosed", StringComparison.OrdinalIgnoreCase)) return DegradedModePolicy.FailClosed;
            return scope is not null && IsAuthScope(scope) ? DegradedModePolicy.FailClosed : DegradedModePolicy.FailOpen;
        }
        if (Enum.TryParse<DegradedModePolicy>(configured, true, out var parsed)) return parsed;
        return site switch
        {
            DegradedSites.OtpCounters => DegradedModePolicy.FailClosed,
            DegradedSites.PermissionCache => DegradedModePolicy.FailOpen, // "open" == fall back to DB (cache miss)
            DegradedSites.NotificationRateLimit => DegradedModePolicy.FailOpen,
            DegradedSites.TokenBlacklist => DegradedModePolicy.FailOpen,
            DegradedSites.SecuritySignals => DegradedModePolicy.FailOpen,
            _ => DegradedModePolicy.FailClosed
        };
    }

    public DegradedModePolicy For(string site) => Resolve(site, _cfg?[$"Security:RedisDegraded:{site}"]);

    public DegradedModePolicy ForRateLimitScope(string scope)
        => Resolve(DegradedSites.RateLimit, _cfg?[$"Security:RedisDegraded:{DegradedSites.RateLimit}"], scope);

    public void Report(string site, Exception ex)
    {
        Degraded.Add(1, new KeyValuePair<string, object?>("site", site));
        _log?.LogWarning(ex, "Redis degraded at {Site}; policy={Policy}", site, For(site));
    }
}
