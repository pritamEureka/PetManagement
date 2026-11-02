using FluentValidation;
using Pawzaroo.Application.Modules.Adoption.Dtos;

namespace Pawzaroo.Application.Modules.Adoption.Validators;

/// <summary>A listing's point is optional, but when given it must be a complete, valid lat/lng pair.</summary>
internal static class GeoRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, double?> lat, Func<T, double?> lng)
    {
        v.RuleFor(x => lat(x)).InclusiveBetween(-90, 90).When(x => lat(x).HasValue).OverridePropertyName("latitude");
        v.RuleFor(x => lng(x)).InclusiveBetween(-180, 180).When(x => lng(x).HasValue).OverridePropertyName("longitude");
        v.RuleFor(x => x).Must(x => lat(x).HasValue == lng(x).HasValue)
            .WithMessage("Provide both latitude and longitude, or neither.").OverridePropertyName("location");
    }
}

public class CreateAdoptionListingGeoValidator : AbstractValidator<CreateAdoptionListingInput>
{
    public CreateAdoptionListingGeoValidator() => GeoRules.Apply(this, x => x.Latitude, x => x.Longitude);
}

public class UpdateAdoptionListingGeoValidator : AbstractValidator<UpdateAdoptionListingInput>
{
    public UpdateAdoptionListingGeoValidator() => GeoRules.Apply(this, x => x.Latitude, x => x.Longitude);
}
