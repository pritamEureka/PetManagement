using FluentValidation;
using Pawzaroo.Application.Modules.Security.Dtos;

namespace Pawzaroo.Application.Modules.Security.Validators;

public class ReportContentInputValidator : AbstractValidator<ReportContentInput>
{
    public ReportContentInputValidator()
    {
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}

public class ModerationActionInputValidator : AbstractValidator<ModerationActionInput>
{
    public ModerationActionInputValidator()
    {
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(2000);

        // SuspendUntil only meaningful for Suspend / Ban
        RuleFor(x => x.SuspendUntil)
            .Must((input, until) =>
                until is null
                || input.Action is Domain.Moderation.ModerationActionType.Suspend
                                  or Domain.Moderation.ModerationActionType.Ban)
            .WithMessage("SuspendUntil is only valid for Suspend / Ban actions.");
    }
}

public class VerifyOtpInputValidator : AbstractValidator<VerifyOtpInput>
{
    public VerifyOtpInputValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6,8}$").WithMessage("Code must be 6–8 digits.");
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(256);
    }
}
