using System.Text.RegularExpressions;
using FluentValidation;
using Pawzaroo.Application.Modules.Pets.Dtos;

namespace Pawzaroo.Application.Modules.Pets.Validators;

internal static class PetRules
{
    public static readonly Regex DoseTimes = new(@"^([01]\d|2[0-3]):[0-5]\d(\s*,\s*([01]\d|2[0-3]):[0-5]\d){0,7}$", RegexOptions.Compiled);

    public static bool NotInFuture(DateTime d) => d <= DateTime.UtcNow.AddDays(1);
    public static bool NotInFuture(DateTime? d) => d is null || NotInFuture(d.Value);
    public static bool IsHttpUrl(string? s) =>
        string.IsNullOrWhiteSpace(s)
        || (Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps));
}

public class UpsertPetInputValidator : AbstractValidator<UpsertPetInput>
{
    public UpsertPetInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AnimalType).IsInEnum();
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x.Breed).MaximumLength(128);
        RuleFor(x => x.Color).MaximumLength(64);
        RuleFor(x => x.TagNumber).MaximumLength(64);
        RuleFor(x => x.MicrochipNumber).MaximumLength(64);
        RuleFor(x => x.Allergies).MaximumLength(1000);
        RuleFor(x => x.DietNotes).MaximumLength(2000);
        RuleFor(x => x.WeightKg).GreaterThan(0).LessThan(5000).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.BirthDate).Must(PetRules.NotInFuture).WithMessage("Birth date can't be in the future.");
        RuleFor(x => x.PrimaryPhotoUrl).Must(PetRules.IsHttpUrl).WithMessage("Photo must be an http(s) URL.");
        RuleFor(x => x).Must(x => x.SireId is null || x.SireId != x.DamId)
            .WithMessage("Sire and dam must be different animals.");
    }
}

public class VaccinationInputValidator : AbstractValidator<VaccinationInput>
{
    public VaccinationInputValidator()
    {
        RuleFor(x => x.VaccineName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AdministeredOn).Must(PetRules.NotInFuture).WithMessage("Administered date can't be in the future.");
        RuleFor(x => x.NextDueOn).GreaterThan(x => x.AdministeredOn).When(x => x.NextDueOn.HasValue)
            .WithMessage("Next due date must be after the administered date.");
        RuleFor(x => x.AdministeredByVet).MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.CertificateUrl).Must(PetRules.IsHttpUrl);
    }
}

public class MedicalRecordInputValidator : AbstractValidator<MedicalRecordInput>
{
    public MedicalRecordInputValidator()
    {
        RuleFor(x => x.VisitDate).Must(PetRules.NotInFuture).WithMessage("Visit date can't be in the future.");
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Diagnosis) || !string.IsNullOrWhiteSpace(x.Treatment) || !string.IsNullOrWhiteSpace(x.Notes))
            .WithMessage("Provide a diagnosis, treatment or notes.");
        RuleFor(x => x.Diagnosis).MaximumLength(1000);
        RuleFor(x => x.Treatment).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.VetName).MaximumLength(128);
        RuleFor(x => x.AttachmentUrl).Must(PetRules.IsHttpUrl);
        RuleFor(x => x.PrescriptionFileUrl).Must(PetRules.IsHttpUrl);
    }
}

public class GroomingRecordInputValidator : AbstractValidator<GroomingRecordInput>
{
    public GroomingRecordInputValidator()
    {
        RuleFor(x => x.ServiceType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Provider).MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
    }
}

public class PetPhotoInputValidator : AbstractValidator<PetPhotoInput>
{
    public PetPhotoInputValidator()
    {
        RuleFor(x => x.Url).NotEmpty().Must(PetRules.IsHttpUrl).WithMessage("Photo must be an http(s) URL.");
        RuleFor(x => x.Caption).MaximumLength(256);
    }
}

public class WeightLogInputValidator : AbstractValidator<WeightLogInput>
{
    public WeightLogInputValidator()
    {
        RuleFor(x => x.WeightKg).GreaterThan(0).LessThan(5000);
        RuleFor(x => x.RecordedOn).Must(PetRules.NotInFuture).WithMessage("Date can't be in the future.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class MedicationInputValidator : AbstractValidator<MedicationInput>
{
    public MedicationInputValidator()
    {
        RuleFor(x => x.DrugName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Dose).MaximumLength(100);
        RuleFor(x => x.DoseTimes).NotEmpty().Matches(PetRules.DoseTimes)
            .WithMessage("Dose times must be HH:mm values separated by commas, e.g. 08:00, 20:00.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue);
        RuleFor(x => x.Instructions).MaximumLength(1000);
    }
}

public class DoseLogInputValidator : AbstractValidator<DoseLogInput>
{
    public DoseLogInputValidator() => RuleFor(x => x.Notes).MaximumLength(500);
}

public class InviteCaretakerInputValidator : AbstractValidator<InviteCaretakerInput>
{
    public InviteCaretakerInputValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ExpiresAt).GreaterThan(DateTime.UtcNow).When(x => x.ExpiresAt.HasValue);
    }
}

public class CareRoutineInputValidator : AbstractValidator<CareRoutineInput>
{
    public CareRoutineInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ActivityType).IsInEnum();
        RuleFor(x => x.Details).MaximumLength(500);
    }
}

public class CareLogInputValidator : AbstractValidator<CareLogInput>
{
    public CareLogInputValidator()
    {
        RuleFor(x => x.ActivityType).IsInEnum();
        RuleFor(x => x.PerformedAt).Must(PetRules.NotInFuture).WithMessage("Can't log care in the future.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class ExpenseInputValidator : AbstractValidator<ExpenseInput>
{
    public ExpenseInputValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0).LessThan(10_000_000);
        RuleFor(x => x.SpentOn).Must(PetRules.NotInFuture).WithMessage("Date can't be in the future.");
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class InsurancePolicyInputValidator : AbstractValidator<InsurancePolicyInput>
{
    public InsurancePolicyInputValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PolicyNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CoverageSummary).MaximumLength(2000);
        RuleFor(x => x.CoverageLimit).GreaterThanOrEqualTo(0).When(x => x.CoverageLimit.HasValue);
        RuleFor(x => x.Premium).GreaterThanOrEqualTo(0).When(x => x.Premium.HasValue);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).When(x => x.EndDate.HasValue);
        RuleFor(x => x.DocumentUrl).Must(PetRules.IsHttpUrl);
    }
}

public class CreateShareLinkInputValidator : AbstractValidator<CreateShareLinkInput>
{
    public CreateShareLinkInputValidator()
    {
        RuleFor(x => x.ValidDays).InclusiveBetween(1, 90);
        RuleFor(x => x.Label).MaximumLength(128);
    }
}

public class SightingInputValidator : AbstractValidator<SightingInput>
{
    public SightingInputValidator()
    {
        RuleFor(x => x.Message).MaximumLength(1000);
        RuleFor(x => x.ContactInfo).MaximumLength(200);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public class PetGroupInputValidator : AbstractValidator<PetGroupInput>
{
    public PetGroupInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AnimalType).IsInEnum();
        RuleFor(x => x.Purpose).MaximumLength(128);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class ProductionLogInputValidator : AbstractValidator<ProductionLogInput>
{
    public ProductionLogInputValidator()
    {
        RuleFor(x => x).Must(x => x.PetGroupId.HasValue || x.PetId.HasValue).WithMessage("Choose a group or an animal.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(32);
        RuleFor(x => x.RecordedOn).Must(PetRules.NotInFuture);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class BreedingRecordInputValidator : AbstractValidator<BreedingRecordInput>
{
    public BreedingRecordInputValidator()
    {
        RuleFor(x => x.SireId).NotEqual(x => x.DamId).When(x => x.SireId.HasValue);
        RuleFor(x => x.SireDescription).MaximumLength(256);
        RuleFor(x => x.ExpectedDueOn).GreaterThan(x => x.MatedOn).When(x => x.ExpectedDueOn.HasValue);
        RuleFor(x => x.OffspringCount).InclusiveBetween(0, 100).When(x => x.OffspringCount.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
