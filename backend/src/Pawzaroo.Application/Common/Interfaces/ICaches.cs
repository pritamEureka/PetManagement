namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>RBAC: permission set cache keyed by userId.</summary>
public interface IPermissionCache
{
    Task<IReadOnlyCollection<string>?> GetAsync(Guid userId, CancellationToken ct = default);
    Task SetAsync(Guid userId, IReadOnlyCollection<string> permissions, CancellationToken ct = default);
    Task InvalidateAsync(Guid userId, CancellationToken ct = default);
    Task InvalidateAllAsync(CancellationToken ct = default);
}

/// <summary>Access-token blacklist + refresh-session lookup.</summary>
public interface ISessionCache
{
    Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken ct = default);

    Task TrackRefreshAsync(string refreshJti, Guid userId, TimeSpan ttl, CancellationToken ct = default);
    Task<Guid?> ResolveRefreshAsync(string refreshJti, CancellationToken ct = default);
    Task RevokeRefreshAsync(string refreshJti, CancellationToken ct = default);
}

/// <summary>One-time-passcode cache with attempt counter.</summary>
public interface IOtpCache
{
    Task SetAsync(string subject, string code, TimeSpan ttl, CancellationToken ct = default);
    Task<string?> GetAsync(string subject, CancellationToken ct = default);
    Task<int> RecordAttemptAsync(string subject, CancellationToken ct = default);
    Task ClearAsync(string subject, CancellationToken ct = default);
}

/// <summary>Per-user notification unread counter + last fetch timestamp.</summary>
public interface INotificationCountCache
{
    Task<long> GetUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<long> BumpUnreadAsync(Guid userId, int delta = 1, CancellationToken ct = default);
    Task ResetUnreadAsync(Guid userId, CancellationToken ct = default);

    Task<DateTime?> GetLastFetchAsync(Guid userId, CancellationToken ct = default);
    Task SetLastFetchAsync(Guid userId, DateTime at, CancellationToken ct = default);
}

/// <summary>
/// Distributed token-bucket / fixed-window rate limiter backed by Redis. Used
/// for cross-instance limits (single-instance limits stay on .NET RateLimiter).
/// </summary>
public interface IRedisRateLimiter
{
    /// <summary>Returns true if the request is allowed and decrements the budget.</summary>
    Task<RateLimitDecision> CheckAsync(string scope, string partition, int permitLimit, TimeSpan window, CancellationToken ct = default);
}

public record RateLimitDecision(bool Allowed, long Remaining, TimeSpan ResetIn);

/// <summary>Per-doctor/day cached availability — invalidated when the doctor edits hours.</summary>
public interface IDoctorAvailabilityCache
{
    Task<T?> GetSlotsAsync<T>(Guid doctorId, DateOnly date, CancellationToken ct = default);
    Task SetSlotsAsync<T>(Guid doctorId, DateOnly date, T slots, CancellationToken ct = default);
    Task InvalidateForDoctorAsync(Guid doctorId, CancellationToken ct = default);
}
