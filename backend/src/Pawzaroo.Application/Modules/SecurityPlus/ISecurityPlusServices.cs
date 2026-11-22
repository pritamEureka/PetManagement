namespace Pawzaroo.Application.Modules.SecurityPlus;

public record SessionDto(Guid Id, DateTime CreatedAt, DateTime ExpiresAt, string? IpAddress,
    string? DeviceLabel, Guid? DeviceId, bool DeviceTrusted, DateTime? LastSeenAt);

public record RecoveryCodesDto(IReadOnlyList<string> Codes, int Remaining);
public record RecoveryStatusDto(int Remaining, int Total, DateTime? LastGeneratedAt);

public record RecordConsentInput(string ConsentType, string? Version, bool Granted);
public record ConsentDto(Guid Id, string ConsentType, string? Version, bool Granted, string? IpAddress, DateTime RecordedAt);

public record StepUpInput(string? Password, string? OtpCode);

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> ListAsync(CancellationToken ct = default);
    /// <summary>Id of the session (refresh token) matching the caller's own refresh token, or null if it is unknown/inactive.</summary>
    Task<Guid?> FindCurrentAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(Guid sessionId, CancellationToken ct = default);
    /// <summary>Revokes every active refresh token; returns how many were revoked.</summary>
    Task<int> RevokeAllAsync(CancellationToken ct = default);
    Task SetDeviceTrustedAsync(Guid deviceId, bool trusted, CancellationToken ct = default);
}

public interface IRecoveryCodeService
{
    Task<RecoveryStatusDto> StatusAsync(CancellationToken ct = default);
    /// <summary>Replaces all existing codes with a fresh set; plaintext is returned once.</summary>
    Task<RecoveryCodesDto> GenerateAsync(CancellationToken ct = default);
    /// <summary>Consumes a code if valid and unused.</summary>
    Task<bool> ConsumeAsync(Guid userId, string code, CancellationToken ct = default);
}

public interface IConsentHistoryService
{
    Task<ConsentDto> RecordAsync(RecordConsentInput input, string? ip, string? userAgent, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentDto>> ListMineAsync(CancellationToken ct = default);
}

/// <summary>
/// Reusable step-up check. Inject into any service/controller guarding a sensitive
/// action (ownership transfer, refunds...) and call RequireRecentAsync;
/// it throws ForbiddenException("step_up_required") when the user has not
/// re-authenticated within the window (5 minutes).
/// </summary>
public interface IStepUpAuthService
{
    static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    Task<bool> VerifyAsync(StepUpInput input, CancellationToken ct = default);
    Task<bool> HasRecentAsync(Guid userId, CancellationToken ct = default);
    Task RequireRecentAsync(CancellationToken ct = default);
}
