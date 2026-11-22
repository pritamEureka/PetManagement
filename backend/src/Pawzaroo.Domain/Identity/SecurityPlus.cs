using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

/// <summary>One-time backup recovery code. Only a SHA-256 hash is stored.</summary>
public class BackupRecoveryCode : BaseEntity
{
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
}

/// <summary>Append-only consent ledger. Never updated or deleted by application code.</summary>
public class ConsentRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public string ConsentType { get; set; } = default!;   // e.g. "terms", "privacy", "marketing"
    public string? Version { get; set; }
    public bool Granted { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Records a recent re-authentication (password/OTP) for step-up checks.</summary>
public class StepUpVerification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Method { get; set; } = default!;        // "password" | "otp" | "recovery"
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
