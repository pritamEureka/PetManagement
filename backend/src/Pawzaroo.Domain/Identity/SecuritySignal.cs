using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

/// <summary>A detected suspicious-activity signal (impossible travel, OTP brute force, enumeration, ...).</summary>
public class SecuritySignal : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? Ip { get; set; }
    /// <summary>impossible_travel | otp_failures | account_enumeration | ...</summary>
    public string Type { get; set; } = default!;
    /// <summary>low | medium | high</summary>
    public string Severity { get; set; } = "medium";
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
