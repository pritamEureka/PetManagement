using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

/// <summary>
/// One row per (user, device-fingerprint) pair seen at login. Used to detect
/// concurrent / suspicious sessions, surface "trusted devices" in account
/// settings, and selectively revoke refresh tokens.
///
/// Fingerprint is derived from User-Agent + a stable client-id cookie. Treat
/// the value as a hint, not an identity — clients can rotate it freely.
/// </summary>
public class UserDevice : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Fingerprint { get; set; } = default!;
    public string? Label { get; set; }            // user-visible: "Chrome on MacBook"
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? IpCity { get; set; }
    public string? IpCountry { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt  { get; set; } = DateTime.UtcNow;
    public bool IsTrusted { get; set; }
    public bool IsRevoked { get; set; }
}
