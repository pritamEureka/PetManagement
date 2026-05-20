using FluentValidation.TestHelper;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Identity.Features.Register;
using Xunit;

namespace Pawzaroo.Tests.Unit.Identity;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand Valid() => new(
        Email: "new.user@example.com",
        Password: "Password1",
        DisplayName: "New User",
        PhoneNumber: "+8801712345678",
        Ip: "127.0.0.1",
        RequestedRole: SystemRoles.User);

    [Fact]
    public void valid_self_registration_passes()
    {
        var result = _validator.TestValidate(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void invalid_email_fails(string email)
    {
        var result = _validator.TestValidate(Valid() with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("password1")]
    [InlineData("PASSWORD1")]
    [InlineData("Password")]
    [InlineData("Pass1")]
    public void weak_password_fails(string password)
    {
        var result = _validator.TestValidate(Valid() with { Password = password });

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void privileged_role_cannot_self_register()
    {
        var result = _validator.TestValidate(Valid() with { RequestedRole = SystemRoles.Admin });

        result.ShouldHaveValidationErrorFor(x => x.RequestedRole);
    }

    [Fact]
    public void empty_requested_role_is_allowed_and_defaults_later()
    {
        var result = _validator.TestValidate(Valid() with { RequestedRole = null });

        result.ShouldNotHaveValidationErrorFor(x => x.RequestedRole);
    }
}
