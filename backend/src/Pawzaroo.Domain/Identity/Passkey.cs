using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Identity;

/// <summary>A registered WebAuthn credential (passkey). Public key is stored as DER SubjectPublicKeyInfo (ES256 / P-256).</summary>
public class PasskeyCredential : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Credential id, base64url encoded (unique).</summary>
    public string CredentialId { get; set; } = default!;
    public byte[] PublicKey { get; set; } = default!;
    public long SignCount { get; set; }
    public string Label { get; set; } = "Passkey";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
