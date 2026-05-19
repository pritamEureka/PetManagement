using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;

namespace Pawzaroo.Application.Modules.Security.Services;

/// <summary>
/// Reports: anyone authenticated may file one; admins/mods view and resolve.
/// </summary>
public interface IReportService
{
    Task<Guid> CreateAsync(ReportContentInput input, CancellationToken ct = default);

    Task<IReadOnlyList<ContentReportDto>> ListAsync(
        ReportStatus? status, ReportTargetType? targetType, int page, int pageSize,
        CancellationToken ct = default);

    Task<ContentReportDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task SetStatusAsync(Guid id, ReportStatus status, string? notes, CancellationToken ct = default);
}

/// <summary>
/// Single entry point for every moderator action. Behind the scenes it can
/// open a suspension, file a warning, hide content, or escalate — all
/// recorded as a <see cref="ModerationAction"/> for audit.
/// </summary>
public interface IModerationService
{
    Task<ModerationActionDto> TakeActionAsync(ModerationActionInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ModerationActionDto>> HistoryAsync(
        ModerationTargetType targetType, Guid targetId, CancellationToken ct = default);
}

/// <summary>User-side: device list, revoke, trust, lookup current device.</summary>
public interface IUserDeviceService
{
    Task<UserDevice> TrackOnLoginAsync(Guid userId, string? userAgent, string? ip,
        string? clientFingerprint, CancellationToken ct = default);

    Task<IReadOnlyList<UserDeviceDto>> ListMineAsync(CancellationToken ct = default);
    Task RevokeAsync(Guid deviceId, CancellationToken ct = default);
    Task TrustAsync(Guid deviceId, string? label, CancellationToken ct = default);
}

/// <summary>
/// Account hold / strikes. Used by AdminController + ModerationService.
/// </summary>
public interface IUserDisciplineService
{
    Task<UserSuspension> SuspendAsync(Guid userId, string reason, string? details,
        DateTime? expiresAt, bool isBan, CancellationToken ct = default);

    Task LiftAsync(Guid suspensionId, string? notes, CancellationToken ct = default);
    Task<UserSuspensionDto?> GetActiveAsync(Guid userId, CancellationToken ct = default);

    Task<UserWarning> WarnAsync(Guid userId, WarningSeverity severity, string reason,
        string? message, string? relatedContentType, Guid? relatedContentId, CancellationToken ct = default);

    Task AcknowledgeWarningAsync(Guid warningId, CancellationToken ct = default);
    Task<IReadOnlyList<UserWarningDto>> ListWarningsForUserAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Privileged actions log. Append-only — only admins can read.
/// </summary>
public interface IAdminActionLogger
{
    Task LogAsync(string action, string targetType, string? targetId,
        string? reason, object? payload, CancellationToken ct = default);

    Task<IReadOnlyList<AdminActionLogDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// OTP issuance & verification (email/phone/password-reset/2FA). The transport
/// (email, SMS) is pluggable behind <see cref="IOtpDeliveryService"/>.
/// </summary>
public interface IOtpService
{
    Task IssueAsync(Guid userId, OtpPurpose purpose, string destination, CancellationToken ct = default);
    Task<bool> VerifyAsync(Guid userId, OtpPurpose purpose, string code, CancellationToken ct = default);
}

public interface IOtpDeliveryService
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default);
    Task SendSmsAsync(string toPhone, string body, CancellationToken ct = default);
}

/// <summary>TOTP (RFC 6238) two-factor authentication, optional for admins.</summary>
public interface ITwoFactorService
{
    Task<TwoFactorSetupDto> BeginSetupAsync(CancellationToken ct = default);
    Task<bool> ConfirmEnableAsync(string code, CancellationToken ct = default);
    Task<bool> DisableAsync(string code, CancellationToken ct = default);
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// File upload validator — extension/MIME sniffing, max size, basic malware
/// scan hook. Production should delegate the malware check to ClamAV / S3
/// pre-signed callback into a sandbox.
/// </summary>
public interface IFileValidationService
{
    FileValidationResult Validate(string fileName, string? contentType, long sizeBytes, ReadOnlySpan<byte> headBytes);

    /// <summary>
    /// Pre-flight check used before issuing a presigned upload URL. We don't
    /// have the file bytes yet, so we can only check the extension allowlist
    /// and the claimed Content-Type against the allowed MIME prefixes.
    /// Full magic-byte validation happens server-side after upload.
    /// </summary>
    FileValidationResult ValidatePreflight(string fileName, string? contentType);

    Task<FileValidationResult> ScanAsync(Stream content, CancellationToken ct = default);
}
