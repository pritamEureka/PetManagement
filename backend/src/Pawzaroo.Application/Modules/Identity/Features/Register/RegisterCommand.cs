using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Application.Modules.Identity.Features.Register;

/// <summary>
/// Result of self-service registration. We deliberately do NOT issue tokens here:
/// every new account starts in ApprovalStatus.Pending and an admin must approve
/// it before login is allowed.
/// </summary>
public record RegistrationResult(Guid UserId, string Email, string Status, string Message);

/// <summary>
/// Roles a registrant may pick at sign-up. Privileged roles (SuperAdmin / Admin /
/// Moderator / SupportAgent) are intentionally NOT in this list — only a SuperAdmin
/// can grant those through the role-assignment surface.
/// </summary>
public static class SelfRegisterRoles
{
    public static readonly string[] Allowed =
    {
        SystemRoles.User,            // Pet Owner (default)
        SystemRoles.StoreOwner,
        SystemRoles.Veterinarian,
        SystemRoles.Seller,
        SystemRoles.ServiceProvider,
        SystemRoles.Breeder,
        SystemRoles.AdoptionCenter,
        SystemRoles.DeliveryUser,
    };

    public static bool IsAllowed(string role) => Array.IndexOf(Allowed, role) >= 0;
}

public record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? PhoneNumber,
    string? Ip,
    string? RequestedRole = null) : IRequest<RegistrationResult>;

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
        RuleFor(x => x.RequestedRole!)
            .Must(SelfRegisterRoles.IsAllowed)
            .When(x => !string.IsNullOrEmpty(x.RequestedRole))
            .WithMessage("Selected role is not available for self-registration.");
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

        // Pick the requested role if it's one the user is allowed to self-select;
        // otherwise default to Pet Owner. SuperAdmin / Admin / Moderator /
        // SupportAgent are not in SelfRegisterRoles.Allowed — only a SuperAdmin
        // can grant those, and that goes through UsersAdminController.GrantRole.
        var requested = (req.RequestedRole?.Trim() is { Length: > 0 } r && SelfRegisterRoles.IsAllowed(r))
            ? r : SystemRoles.User;

        var role = await _db.Roles.SingleOrDefaultAsync(x => x.Name == requested, ct)
            ?? throw new ConflictException($"System role '{requested}' is not seeded — contact an administrator.");
        _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync(ct);

        // Notify every admin / super-admin so the new account is visible in the
        // approval queue without polling. Best-effort: failures don't roll back.
        await NotifyAdminsAsync(user, requested, ct);

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

    private async Task NotifyAdminsAsync(User user, string requestedRole, CancellationToken ct)
    {
        var adminIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Name == SystemRoles.SuperAdmin || ur.Role.Name == SystemRoles.Admin)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (adminIds.Count == 0) return;

        var title = "New registration awaiting approval";
        var body = $"{user.DisplayName} ({user.Email}) signed up as {requestedRole}.";

        // Persist one InAppNotification row per admin so the message survives a
        // missed SignalR push (offline admin) and is visible from the bell list /
        // /admin/notifications. The Url deep-links the bell into the queue.
        foreach (var adminId in adminIds)
        {
            _db.Notifications.Add(new InAppNotification
            {
                UserId = adminId,
                Title = title,
                Body = body,
                Url = "/admin/approvals",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    kind = "registration_pending",
                    userId = user.Id,
                    email = user.Email,
                    requestedRole
                })
            });
        }
        await _db.SaveChangesAsync(ct);

        // Real-time push for any admins currently connected. Failures here are
        // non-critical because the persisted row above is the durable channel.
        foreach (var adminId in adminIds)
        {
            try
            {
                await _notify.NotifyUserAsync(adminId, title, body,
                    new { url = "/admin/approvals", userId = user.Id, email = user.Email, requestedRole }, ct);
            }
            catch { /* notification is non-critical */ }
        }
    }
}
