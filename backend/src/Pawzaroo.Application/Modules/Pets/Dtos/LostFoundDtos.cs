using FluentValidation;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Application.Modules.Pets.Dtos;

public record LostFoundQuery(LostFoundKind? Kind, AnimalType? AnimalType, string? City, LostFoundStatus? Status,
    string? Q, int Page = 1, int PageSize = 20);

public record CreateLostFoundInput(
    LostFoundKind Kind, Guid? PetId, AnimalType? AnimalType, string? Breed, string? Color, string Description,
    string? PhotoUrl, DateTime LastSeenAt, string City, string? Area, double? Latitude, double? Longitude,
    ContactPreference ContactPreference, string? ContactPhone, bool ShareToFeed = true);

public record ResolveLostFoundInput(bool Reunited, string? Note);

public record LostFoundSummaryDto(
    Guid Id, LostFoundKind Kind, LostFoundStatus Status, AnimalType AnimalType, string? Breed, string? Color,
    string Description, string? PhotoUrl, DateTime LastSeenAt, string City, string? Area,
    Guid ReporterId, string ReporterName, Guid? PetId, string? PetName, DateTime CreatedAt);

public record LostFoundDetailDto(
    LostFoundSummaryDto Report, double? Latitude, double? Longitude, ContactPreference ContactPreference,
    string? ContactPhone, string? MicrochipNumber, string? TagNumber, DateTime? ResolvedAt, Guid? FeedPostId, bool IsMine);

public record LostFoundMatchDto(LostFoundSummaryDto Report, int Score, IReadOnlyList<string> Reasons);

public record PagedLostFound(IReadOnlyList<LostFoundSummaryDto> Items, int Total, int Page, int PageSize);

public class CreateLostFoundInputValidator : AbstractValidator<CreateLostFoundInput>
{
    public CreateLostFoundInputValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x).Must(x => x.PetId.HasValue || x.AnimalType.HasValue)
            .WithMessage("Pick one of your pets or choose the animal type.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Breed).MaximumLength(128);
        RuleFor(x => x.Color).MaximumLength(64);
        RuleFor(x => x.City).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Area).MaximumLength(128);
        RuleFor(x => x.ContactPhone).MaximumLength(32);
        RuleFor(x => x.LastSeenAt).LessThanOrEqualTo(_ => DateTime.UtcNow.AddHours(1)).WithMessage("Last-seen time can't be in the future.");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.PhotoUrl).Must(u => string.IsNullOrWhiteSpace(u) || Uri.TryCreate(u, UriKind.Absolute, out _));
    }
}
