using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Modules.Identity.Features.Register;

/// <summary>
/// Result of self-service registration. We deliberately do NOT issue tokens here:
/// every new account starts in ApprovalStatus.Pending and an admin must approve
/// it before login is allowed.
/// </summary>
public record RegistrationResult(Guid UserId, string Email, string Status, string Message);

public record RegisterCommand(string Email, string Password, string DisplayName, string? PhoneNumber, string? Ip)
    : IRequest<RegistrationResult>;

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

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegistrationResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly INotificationService _notify;
    private readonly IEmailService _email;

    public RegisterCommandHandler(IApplicationDbContext db, IPasswordHasher hasher,
        INotificationService notify, IEmailService email)
    {
        _db = db;
        _hasher = hasher;
        _notify = notify;
        _email = email;
    }

    public async Task<RegistrationResult> HandleAsync(RegisterCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Email already registered.");

        // Pending until an admin approves. IsActive stays true so that admins can
        // edit / restore the row through the existing user-admin surfaces; login
        // is gated by ApprovalStatus, not IsActive.
        var user = new User
        {
            Email = email,
            DisplayName = req.DisplayName,
            PhoneNumber = req.PhoneNumber,
            PasswordHash = _hasher.Hash(req.Password),
            EmailConfirmed = false,
            IsActive = true,
            ApprovalStatus = ApprovalStatus.Pending
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // Self-registration is always end-user role. Admin/SuperAdmin grants must
        // go through the role-assignment surface (which is SuperAdmin-only for the
        // admin tier — enforced in UsersAdminController).
        var userRole = await _db.Roles.SingleOrDefaultAsync(r => r.Name == SystemRoles.User, ct)
            ?? throw new ConflictException("System role 'User' is not seeded — contact an administrator.");
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await _db.SaveChangesAsync(ct);

        // Notify every admin / super-admin so the new account is visible in the
        // approval queue without polling. Best-effort: failures don't roll back.
        await NotifyAdminsAsync(user, ct);

        // Confirmation email to the new user — only logged in the dev stub, but
        // the message itself is sized for real delivery.
        await _email.SendAsync(user.Email,
            "Your Pawzaroo registration is pending approval",
            $"Hi {user.DisplayName},\n\nWe've received your sign-up request. An administrator will review it shortly and " +
            "you'll get another email when your account is approved. After that you'll be able to log in.\n\n— Pawzaroo",
            user.Id, ct);

        return new RegistrationResult(
            UserId: user.Id,
            Email: user.Email,
            Status: nameof(ApprovalStatus.Pending),
            Message: "Your registration request was submitted. An administrator will review it shortly; " +
                     "we'll email you at the address you provided once it's approved.");
    }

    private async Task NotifyAdminsAsync(User user, CancellationToken ct)
    {
        var adminIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Name == SystemRoles.SuperAdmin || ur.Role.Name == SystemRoles.Admin)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var adminId in adminIds)
        {
            try
            {
                await _notify.NotifyUserAsync(adminId,
                    "New registration awaiting approval",
                    $"{user.DisplayName} ({user.Email}) just signed up.",
                    new { userId = user.Id, email = user.Email }, ct);
            }
            catch { /* notification is non-critical */ }
        }
    }
}
