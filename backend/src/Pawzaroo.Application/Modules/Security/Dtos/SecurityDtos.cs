using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;

namespace Pawzaroo.Application.Modules.Security.Dtos;

// ---------- Reports / moderation ------------------------------------------

public record ReportContentInput(
    ReportTargetType TargetType,
    Guid TargetId,
    string Reason,
    string? Details);

public record ContentReportDto(
    Guid Id,
    ReportTargetType TargetType,
    Guid TargetId,
    Guid ReporterId,
    string ReporterDisplayName,
    string Reason,
    string? Details,
    ReportStatus Status,
    Guid? ResolvedById,
    string? ResolutionNotes,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

public record ModerationActionInput(
    ModerationActionType Action,
    ModerationTargetType TargetType,
    Guid TargetId,
    Guid? ReportId,
    string? Notes,
    // Optional Suspend / Ban / Warn extras:
    DateTime? SuspendUntil,
    bool? IsBan,
    WarningSeverity? WarningSeverity);

public record ModerationActionDto(
    Guid Id,
    ModerationActionType Action,
    ModerationTargetType TargetType,
    Guid TargetId,
    Guid ModeratorId,
    string ModeratorName,
    string? Notes,
    Guid? RelatedSuspensionId,
    Guid? RelatedWarningId,
    DateTime CreatedAt);

// ---------- Devices --------------------------------------------------------

public record UserDeviceDto(
    Guid Id,
    string Fingerprint,
    string? Label,
    string? UserAgent,
    string? IpAddress,
    string? IpCity,
    string? IpCountry,
    DateTime FirstSeenAt,
    DateTime LastSeenAt,
    bool IsTrusted,
    bool IsRevoked);

// ---------- Warnings / suspensions ----------------------------------------

public record UserWarningDto(
    Guid Id,
    Guid UserId,
    WarningSeverity Severity,
    string Reason,
    string? Message,
    bool AcknowledgedByUser,
    DateTime CreatedAt);

public record UserSuspensionDto(
    Guid Id,
    Guid UserId,
    string Reason,
    string? Details,
    bool IsBan,
    DateTime? ExpiresAt,
    SuspensionStatus Status,
    Guid IssuedById,
    DateTime CreatedAt);

// ---------- Admin action log ----------------------------------------------

public record AdminActionLogDto(
    Guid Id,
    DateTime At,
    Guid AdminId,
    string AdminName,
    string Action,
    string TargetType,
    string? TargetId,
    string? Reason,
    string? IpAddress);

// ---------- OTP / 2FA / password reset ------------------------------------

public record StartVerificationInput(OtpPurpose Purpose, string Destination);
public record VerifyOtpInput(OtpPurpose Purpose, string Destination, string Code);
public record EnableTwoFactorInput(string Code);
public record DisableTwoFactorInput(string Code);
public record TwoFactorSetupDto(string SecretBase32, string OtpAuthUri, IReadOnlyList<string> RecoveryCodes);

// ---------- File validation -----------------------------------------------

public record FileValidationResult(bool Allowed, string? Reason);
