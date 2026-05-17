using FluentValidation.TestHelper;
using Pawzaroo.Application.Modules.Identity.Features.Login;
using Xunit;

namespace Pawzaroo.Tests.Unit.Identity;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _v = new();

    [Fact]
    public void valid_login_passes()
    {
        var result = _v.TestValidate(new LoginCommand("user@example.com", "Password1!", "127.0.0.1"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void invalid_email_fails(string email)
    {
        var result = _v.TestValidate(new LoginCommand(email, "Password1!", null));
        result.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Fact]
    public void empty_password_fails()
    {
        var result = _v.TestValidate(new LoginCommand("user@example.com", "", null));
        result.ShouldHaveValidationErrorFor(c => c.Password);
    }
}
