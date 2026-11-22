namespace Pawzaroo.Infrastructure.Identity;

public class JwtSettings
{
    public string Issuer { get; set; } = "pawzaroo";
    public string Audience { get; set; } = "pawzaroo.clients";
    public string SigningKey { get; set; } = default!;
    /// <summary>Validation-only keys for rotation (config also accepts a comma-separated string; see <see cref="JwtKeyRing"/>).</summary>
    public string[] PreviousSigningKeys { get; set; } = Array.Empty<string>();
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
