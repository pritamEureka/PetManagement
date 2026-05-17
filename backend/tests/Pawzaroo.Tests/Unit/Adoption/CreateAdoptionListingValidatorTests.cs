using FluentAssertions;
using FluentValidation.TestHelper;
using Pawzaroo.Application.Modules.Adoption.Features.CreateAdoptionListing;
using Pawzaroo.Domain.Common;
using Xunit;

namespace Pawzaroo.Tests.Unit.Adoption;

public class CreateAdoptionListingValidatorTests
{
    private readonly CreateAdoptionListingValidator _v = new();

    private static CreateAdoptionListingCommand Valid() => new(
        Title: "Adopt a friendly retriever",
        Description: "Two-year-old, very social.",
        AnimalType: AnimalType.Dog,
        Breed: "Labrador",
        AgeMonths: 24,
        Gender: Gender.Male,
        Vaccinated: true,
        VaccinationDetails: "Rabies, DHPP",
        HealthCondition: "Healthy",
        Location: "Austin, TX",
        AdoptionFee: 0,
        ContactPreference: ContactPreference.Chat,
        PetId: null,
        PhotoUrls: new[] { "https://example.com/a.jpg" });

    [Fact]
    public void valid_command_passes()
    {
        var result = _v.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void empty_title_fails()
    {
        var cmd = Valid() with { Title = "" };
        _v.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Title);
    }

    [Fact]
    public void negative_fee_fails()
    {
        var cmd = Valid() with { AdoptionFee = -1m };
        _v.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.AdoptionFee);
    }

    [Fact]
    public void age_over_50_years_fails()
    {
        var cmd = Valid() with { AgeMonths = 50 * 12 + 1 };
        _v.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.AgeMonths);
    }

    [Fact]
    public void too_many_photos_fails()
    {
        var cmd = Valid() with { PhotoUrls = Enumerable.Range(0, 13).Select(_ => "x").ToArray() };
        _v.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.PhotoUrls);
    }
}
