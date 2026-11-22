namespace Pawzaroo.Application.Modules.SecurityPlus;

public record SecuritySignalDto(Guid Id, Guid? UserId, string? Ip, string Type, string Severity, string? DetailsJson, DateTime CreatedAt);

/// <summary>Detection layer: impossible travel, repeated OTP failures, account enumeration.</summary>
public interface ISecuritySignalService
{
    /// <summary>
    /// Counts a distinct identifier (email) attempted from an IP on a surface ("login", "recovery", "register").
    /// Returns true when the IP is now throttled for enumeration-like behaviour.
    /// </summary>
    Task<bool> RecordIdentifierAttemptAsync(string? ip, string identifier, string surface, CancellationToken ct = default);

    /// <summary>Compares this login's location with the previous one; records a signal when suspicious.</summary>
    Task AnalyzeLoginAsync(Guid userId, string? ip, CancellationToken ct = default);

    /// <summary>True while the user (or the caller IP) is locked out of OTP verification.</summary>
    Task<bool> IsOtpLockedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Records a failed OTP verify; returns true if this failure triggered a lockout.</summary>
    Task<bool> RecordOtpFailureAsync(Guid userId, string? ip, CancellationToken ct = default);

    Task<IReadOnlyList<SecuritySignalDto>> ListAsync(string? type, string? severity, int page, int pageSize, CancellationToken ct = default);
}
