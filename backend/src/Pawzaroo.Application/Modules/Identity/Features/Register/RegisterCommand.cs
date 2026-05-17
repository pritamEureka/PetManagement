using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Modules.Identity.Features.Register;

public record RegisterCommand(string Email, string Password, string DisplayName, string? PhoneNumber, string? Ip)
    : IRequest<AuthResponse>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PhoneNumber).MaximumLength(32).When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenIssuer _tokens;

    public RegisterCommandHandler(IApplicationDbContext db, IPasswordHasher hasher, ITokenIssuer tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResponse> HandleAsync(RegisterCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Email already registered.");

        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName,
            PhoneNumber = req.PhoneNumber,
            PasswordHash = _hasher.Hash(req.Password),
            EmailConfirmed = false,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var userRole = await _db.Roles.FirstAsync(r => r.Name == SystemRoles.User, ct);
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await _db.SaveChangesAsync(ct);

        return await _tokens.IssueAsync(user, req.Ip, ct);
    }
}
