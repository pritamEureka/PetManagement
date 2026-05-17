using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

public enum SuspensionStatus { Active = 0, Lifted = 1, Expired = 2 }

/// <summary>
/// Account hold record. Suspensions can be time-bounded (`ExpiresAt`) or
/// permanent (bans, `IsBan = true`). The active suspension blocks login at the
/// auth layer and is also enforced on every request via SuspensionGuard.
/// </summary>
public class UserSuspension : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Reason { get; set; } = default!;
    public string? Details { get; set; }
    public bool IsBan { get; set; }                    // ban = permanent until explicitly lifted
    public DateTime? ExpiresAt { get; set; }            // null = permanent / until lifted

    public SuspensionStatus Status { get; set; } = SuspensionStatus.Active;

    public Guid IssuedById { get; set; }
    public User IssuedBy { get; set; } = default!;
    public Guid? LiftedById { get; set; }
    public User? LiftedBy { get; set; }
    public DateTime? LiftedAt { get; set; }
}

public enum WarningSeverity { Info = 0, Minor = 1, Major = 2, Final = 3 }

/// <summary>
/// Non-blocking strike. Acknowledgement gates the next login until the user
/// clicks "I understand" on the warning banner — anchors the audit trail.
/// </summary>
public class UserWarning : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public WarningSeverity Severity { get; set; } = WarningSeverity.Minor;
    public string Reason { get; set; } = default!;
    public string? Message { get; set; }
    public string? RelatedContentType { get; set; }  // e.g. "Post", "Comment"
    public Guid? RelatedContentId { get; set; }

    public Guid IssuedById { get; set; }
    public User IssuedBy { get; set; } = default!;

    public bool AcknowledgedByUser { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

/// <summary>
/// Time-based one-time password / email-OTP record. Codes are short and
/// hashed; verification is constant-time. See OtpService.
/// </summary>
public enum OtpPurpose { EmailVerification = 0, PhoneVerification = 1, PasswordReset = 2, TwoFactor = 3 }

public class OtpCode : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public OtpPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Destination { get; set; }       // email or phone the code was sent to
}

/// <summary>
/// Per-user TOTP secret (optional 2FA). Encrypt the seed at rest; this entity
/// stores the encrypted blob plus recovery codes.
/// </summary>
public class TwoFactorSettings : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public bool IsEnabled { get; set; }
    public string? EncryptedSecret { get; set; }      // AES-encrypted TOTP seed
    public string? RecoveryCodesHash { get; set; }    // JSON array of bcrypt-hashed codes
    public DateTime? EnabledAt { get; set; }
}
