using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Modules.Identity.Features.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? Ip,
    string? UserAgent = null,
    string? ClientFingerprint = null,
    string? TwoFactorCode = null) : IRequest<AuthResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenIssuer _tokens;
    private readonly IUserDeviceService _devices;
    private readonly ITwoFactorService _twoFactor;

    public LoginCommandHandler(IApplicationDbContext db, IPasswordHasher hasher, ITokenIssuer tokens,
        IUserDeviceService devices, ITwoFactorService twoFactor)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _devices = devices;
        _twoFactor = twoFactor;
    }

    public async Task<AuthResponse> HandleAsync(LoginCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email, ct)
            ?? throw new ForbiddenException("Invalid credentials.");

        // Verify password before checking active/suspended so timing doesn't leak account existence.
        if (!_hasher.Verify(req.Password, user.PasswordHash))
            throw new ForbiddenException("Invalid credentials.");
        if (!user.IsActive || user.IsSuspended) throw new ForbiddenException("Account disabled.");

        // 2FA gate (admins typically; users may opt in). If enabled, require the code.
        if (await _twoFactor.IsEnabledAsync(user.Id, ct))
        {
            if (string.IsNullOrWhiteSpace(req.TwoFactorCode))
                throw new ForbiddenException("two_factor_required");
            if (!await _twoFactor.VerifyAsync(user.Id, req.TwoFactorCode, ct))
                throw new ForbiddenException("Invalid 2FA code.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Track the device (best-effort — login should not fail if cache is down).
        try { await _devices.TrackOnLoginAsync(user.Id, req.UserAgent, req.Ip, req.ClientFingerprint, ct); }
        catch { /* ignore — device hint is non-critical */ }

        return await _tokens.IssueAsync(user, req.Ip, ct);
    }
}
