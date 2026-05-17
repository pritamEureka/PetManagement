namespace Pawzaroo.Infrastructure.Identity;

public class JwtSettings
{
    public string Issuer { get; set; } = "pawzaroo";
    public string Audience { get; set; } = "pawzaroo.clients";
    public string SigningKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}
