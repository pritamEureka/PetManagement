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

    public RefreshTokenCommandHandler(IApplicationDbContext db, IJwtTokenService jwt, ITokenIssuer tokens)
    {
        _db = db;
        _jwt = jwt;
        _tokens = tokens;
    }

    public async Task<AuthResponse> HandleAsync(RefreshTokenCommand req, CancellationToken ct)
    {
        var hash = _jwt.HashRefreshToken(req.RefreshToken);
        var token = await _db.RefreshTokens.Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == hash, ct)
            ?? throw new ForbiddenException("Invalid refresh token.");
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
