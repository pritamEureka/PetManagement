using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Modules.Identity.Features.Refresh;

public record RefreshTokenCommand(string RefreshToken, string? Ip) : IRequest<AuthResponse>;
public record LogoutCommand(string RefreshToken) : IRequest<Unit>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly ITokenIssuer _tokens;
    private readonly IAuditLogger? _audit;
    private readonly INotificationService? _notify;

    public RefreshTokenCommandHandler(IApplicationDbContext db, IJwtTokenService jwt, ITokenIssuer tokens,
        IAuditLogger? audit = null, INotificationService? notify = null)
    {
        _db = db;
        _jwt = jwt;
        _tokens = tokens;
        _audit = audit;
        _notify = notify;
    }

    /// <summary>
    /// Walks the ReplacedByTokenHash chain forward from <paramref name="startHash"/> and returns every
    /// hash in the descendant chain (including the start). Cycle- and length-safe.
    /// </summary>
    public static IReadOnlyList<string> WalkChain(string startHash, IReadOnlyDictionary<string, string?> replacedBy, int max = 1000)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        string? cur = startHash;
        while (cur is not null && list.Count < max && seen.Add(cur))
        {
            list.Add(cur);
            cur = replacedBy.TryGetValue(cur, out var next) ? next : null;
        }
        return list;
    }

    private async Task RevokeFamilyAsync(Domain.Identity.RefreshToken reused, string? ip, CancellationToken ct)
    {
        var all = await _db.RefreshTokens.Where(rt => rt.UserId == reused.UserId).ToListAsync(ct);
        var map = all.ToDictionary(t => t.TokenHash, t => t.ReplacedByTokenHash, StringComparer.Ordinal);
        var family = WalkChain(reused.TokenHash, map).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var revoked = 0;
        foreach (var t in all.Where(t => family.Contains(t.TokenHash) && t.RevokedAt is null))
        {
            t.RevokedAt = now;
            revoked++;
        }
        await _db.SaveChangesAsync(ct);

        if (_audit is not null)
            await _audit.LogAsync("auth.refresh_reuse_detected", "RefreshToken", reused.Id.ToString(), "Security",
                null, new { reused.UserId, ip, revoked }, ct);
        if (_notify is not null)
            await _notify.NotifyUserAsync(reused.UserId, "Security alert",
                "A previously used sign-in token was presented again, so we signed out the affected session. If this wasn't you, change your password.",
                new { type = "security.refresh_reuse" }, ct);
    }

    public async Task<AuthResponse> HandleAsync(RefreshTokenCommand req, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens.Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash, ct)
            ?? throw new ForbiddenException("Invalid refresh token.");
        // Reuse detection: a token that was already rotated (has a successor) is being replayed.
        // A short grace window tolerates legitimate races (two tabs refreshing at once).
        if (token.RevokedAt is not null && token.ReplacedByTokenHash is not null)
        {
            if (DateTime.UtcNow - token.RevokedAt.Value < TimeSpan.FromSeconds(10))
                throw new ForbiddenException("Refresh token already used.");
            await RevokeFamilyAsync(token, req.Ip, ct);
            throw new ForbiddenException("Refresh token reuse detected.");
        }
        if (token.RevokedAt is not null || DateTime.UtcNow >= token.ExpiresAt)
            throw new ForbiddenException("Expired refresh token.");

        token.RevokedAt = DateTime.UtcNow;
        var fresh = await _tokens.IssueAsync(token.User, req.Ip, ct);
        token.ReplacedByTokenHash = _jwt.HashRefreshToken(fresh.RefreshToken);
        await _db.SaveChangesAsync(ct);
        return fresh;
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwt;
    public LogoutCommandHandler(IApplicationDbContext db, IJwtTokenService jwt) { _db = db; _jwt = jwt; }

    public async Task<Unit> HandleAsync(LogoutCommand req, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == hash, ct);
        if (token is not null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return Unit.Value;
    }
}
