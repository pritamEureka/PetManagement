using FluentValidation.TestHelper;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Validators;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;
using Xunit;

namespace Pawzaroo.Tests.Unit.Security;

public class SecurityValidatorTests
{
    [Fact]
    public void report_content_requires_target_and_reason()
    {
        var validator = new ReportContentInputValidator();
        var input = new ReportContentInput(ReportTargetType.Post, Guid.Empty, "", null);

        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.TargetId);
        validator.TestValidate(input).ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void moderation_suspend_until_only_applies_to_suspend_or_ban()
    {
        var validator = new ModerationActionInputValidator();
        var until = DateTime.UtcNow.AddDays(7);

        validator.TestValidate(new ModerationActionInput(
                ModerationActionType.Warn, ModerationTargetType.User, Guid.NewGuid(), null, null, until, null, WarningSeverity.Minor))
            .ShouldHaveValidationErrorFor(x => x.SuspendUntil);

        validator.TestValidate(new ModerationActionInput(
                ModerationActionType.Suspend, ModerationTargetType.User, Guid.NewGuid(), null, null, until, null, null))
            .ShouldNotHaveValidationErrorFor(x => x.SuspendUntil);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("abcdef")]
    public void verify_otp_rejects_invalid_codes(string code)
    {
        var validator = new VerifyOtpInputValidator();

        validator.TestValidate(new VerifyOtpInput(OtpPurpose.TwoFactor, "user@example.com", code))
            .ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345678")]
    public void verify_otp_accepts_six_to_eight_digit_codes(string code)
    {
        var validator = new VerifyOtpInputValidator();

        validator.TestValidate(new VerifyOtpInput(OtpPurpose.TwoFactor, "user@example.com", code))
            .ShouldNotHaveValidationErrorFor(x => x.Code);
    }
}
