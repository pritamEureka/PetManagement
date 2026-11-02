using FluentAssertions;
using FluentValidation.TestHelper;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Validators;
using Pawzaroo.Domain.Common;
using Xunit;

namespace Pawzaroo.Tests.Unit.Pets;

public class PetValidatorTests
{
    private static UpsertPetInput Pet(DateTime? birth = null, decimal? weight = null, Guid? sire = null, Guid? dam = null, string? photo = null) =>
        new("Rex", AnimalType.Dog, null, Gender.Male, birth, weight, null, null, photo, null, null, false, SireId: sire, DamId: dam);

    [Fact]
    public void pet_rejects_future_birth_date_and_non_positive_weight()
    {
        var v = new UpsertPetInputValidator();
        v.TestValidate(Pet(birth: DateTime.UtcNow.AddDays(10))).ShouldHaveValidationErrorFor(x => x.BirthDate);
        v.TestValidate(Pet(weight: 0)).ShouldHaveValidationErrorFor(x => x.WeightKg);
        v.TestValidate(Pet(birth: DateTime.UtcNow.AddYears(-2), weight: 12.5m)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void pet_rejects_same_animal_as_sire_and_dam_and_non_http_photo()
    {
        var v = new UpsertPetInputValidator();
        var id = Guid.NewGuid();
        v.TestValidate(Pet(sire: id, dam: id)).IsValid.Should().BeFalse();
        v.TestValidate(Pet(photo: "javascript:alert(1)")).ShouldHaveValidationErrorFor(x => x.PrimaryPhotoUrl);
    }

    [Fact]
    public void vaccination_next_due_must_follow_administered_date()
    {
        var v = new VaccinationInputValidator();
        var given = DateTime.UtcNow.Date.AddDays(-1);
        v.TestValidate(new VaccinationInput("Rabies", given, given.AddDays(-1), null, null, null))
            .ShouldHaveValidationErrorFor(x => x.NextDueOn);
        v.TestValidate(new VaccinationInput("Rabies", given, given.AddYears(1), null, null, null))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("08:00", true)]
    [InlineData("08:00, 20:00", true)]
    [InlineData("08:00,14:00,20:00", true)]
    [InlineData("8am", false)]
    [InlineData("25:00", false)]
    [InlineData("", false)]
    public void medication_dose_times_must_be_24h_list(string times, bool valid)
    {
        var input = new MedicationInput("Amoxicillin", "250mg", times, DateTime.UtcNow.Date, null, null);
        new MedicationInputValidator().TestValidate(input).IsValid.Should().Be(valid);
    }

    [Fact]
    public void medical_record_needs_some_clinical_content()
    {
        var v = new MedicalRecordInputValidator();
        v.TestValidate(new MedicalRecordInput(DateTime.UtcNow.Date, null, null, null, "Dr. X", null, null)).IsValid.Should().BeFalse();
        v.TestValidate(new MedicalRecordInput(DateTime.UtcNow.Date, "Otitis", null, null, null, null, null)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(90, true)]
    [InlineData(91, false)]
    public void share_links_are_limited_to_90_days(int days, bool valid)
        => new CreateShareLinkInputValidator().TestValidate(new CreateShareLinkInput(days, null)).IsValid.Should().Be(valid);

    [Fact]
    public void catalog_includes_new_pet_care_permissions()
    {
        var codes = Permissions.All().Select(p => $"{p.Module}.{p.Action}").ToHashSet();
        codes.Should().Contain(new[] { "pets.read_any", "lost_found.view", "lost_found.create", "lost_found.moderate", "services.edit" });
    }
}
