using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Identity;

public class TokenIssuer : ITokenIssuer
{
    private readonly ApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IRedisCacheService _cache;
    private readonly JwtSettings _settings;

    public TokenIssuer(ApplicationDbContext db, IJwtTokenService jwt, IRedisCacheService cache, IOptions<JwtSettings> settings)
    {
        _db = db;
        _jwt = jwt;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<AuthResponse> IssueAsync(User user, string? ip, CancellationToken ct)
    {
        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id).Select(ur => ur.Role.Name).ToListAsync(ct);
        var perms = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Module + "." + rp.Permission.Action)
            .Distinct().ToListAsync(ct);

        await _cache.SetAsync($"perms:{user.Id}", perms.ToArray(), TimeSpan.FromMinutes(10), ct);

        var (access, expires) = _jwt.GenerateAccessToken(user.Id, user.Email, roleNames, perms);
        var refresh = _jwt.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _jwt.HashRefreshToken(refresh),
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenDays),
            CreatedByIp = ip
        });
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, refresh, expires,
            new UserSummary(user.Id, user.Email, user.DisplayName, user.AvatarUrl, roleNames, perms));
    }
}
