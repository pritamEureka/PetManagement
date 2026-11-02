using Pawzaroo.Domain.Common;

namespace Pawzaroo.Application.Modules.Adoption.Dtos;

public record AdoptionListingSummaryDto(
    Guid Id,
    string Title,
    string? PetName,
    AnimalType AnimalType,
    string? Breed,
    Gender Gender,
    AnimalSize? Size,
    int? AgeMonths,
    string? Location,
    decimal AdoptionFee,
    bool Vaccinated,
    bool NeuteredSpayed,
    IReadOnlyList<string> PhotoUrls,
    Guid OwnerId,
    string OwnerDisplayName,
    string? OwnerAvatarUrl,
    AdoptionListingStatus Status,
    bool IsSaved,
    bool IsOwn,
    DateTime CreatedAt,
    /// <summary>Kilometres from the viewer's location; null when either side has no coordinates.</summary>
    double? DistanceKm = null);

public record AdoptionListingDetailDto(
    Guid Id,
    string Title,
    string? PetName,
    string? Description,
    AnimalType AnimalType,
    string? Breed,
    int? AgeMonths,
    Gender Gender,
    AnimalSize? Size,
    string? Color,
    bool Vaccinated,
    string? VaccinationDetails,
    bool NeuteredSpayed,
    string? HealthCondition,
    string? BehaviorNotes,
    bool? GoodWithChildren,
    bool? GoodWithOtherPets,
    string? Location,
    decimal AdoptionFee,
    string? ReasonForListing,
    ContactPreference ContactPreference,
    AdoptionListingStatus Status,
    string? AdminNotes,
    DateTime? SubmittedAt,
    DateTime? DecidedAt,
    DateTime? AdoptedAt,
    Guid OwnerId,
    string OwnerDisplayName,
    string? OwnerAvatarUrl,
    IReadOnlyList<string> PhotoUrls,
    bool IsSaved,
    bool IsOwn,
    int RequestCount,
    DateTime CreatedAt,
    double? Latitude,
    double? Longitude);

public record AdoptionRequestDto(
    Guid Id,
    Guid AdoptionListingId,
    Guid RequesterId,
    string RequesterName,
    string Message,
    AdoptionRequestStatus Status,
    DateTime CreatedAt);

public record CreateAdoptionListingInput(
    string Title,
    string? PetName,
    string? Description,
    AnimalType AnimalType,
    string? Breed,
    int? AgeMonths,
    Gender Gender,
    AnimalSize? Size,
    string? Color,
    bool Vaccinated,
    string? VaccinationDetails,
    bool NeuteredSpayed,
    string? HealthCondition,
    string? BehaviorNotes,
    bool? GoodWithChildren,
    bool? GoodWithOtherPets,
    string? Location,
    decimal AdoptionFee,
    string? ReasonForListing,
    ContactPreference ContactPreference,
    Guid? PetId,
    IReadOnlyList<string>? PhotoUrls,
    bool SubmitForApproval,
    double? Latitude = null,
    double? Longitude = null);

public record UpdateAdoptionListingInput(
    string Title,
    string? PetName,
    string? Description,
    AnimalType AnimalType,
    string? Breed,
    int? AgeMonths,
    Gender Gender,
    AnimalSize? Size,
    string? Color,
    bool Vaccinated,
    string? VaccinationDetails,
    bool NeuteredSpayed,
    string? HealthCondition,
    string? BehaviorNotes,
    bool? GoodWithChildren,
    bool? GoodWithOtherPets,
    string? Location,
    decimal AdoptionFee,
    string? ReasonForListing,
    ContactPreference ContactPreference,
    IReadOnlyList<string>? PhotoUrls,
    double? Latitude = null,
    double? Longitude = null);

public record CreateAdoptionRequestInput(string Message);

public record MarkAdoptedInput(Guid? AdoptedByUserId, bool TransferPetProfile = true, bool IncludeHealthHistory = true);

public record AdminDecisionInput(string? AdminNotes);

public record CreateWantedPostInput(
    AnimalType AnimalType,
    string? Breed,
    int? PreferredAgeMonthsMin,
    int? PreferredAgeMonthsMax,
    AnimalSize? PreferredSize,
    string? PreferredLocation,
    string? ExperienceWithPets,
    HomeEnvironment? HomeEnvironment,
    string? OtherPetsAtHome,
    string? ReasonForAdoption,
    ContactPreference ContactPreference,
    string? Description);

public record WantedPostDto(
    Guid Id,
    Guid RequesterId,
    string RequesterName,
    AnimalType AnimalType,
    string? Breed,
    int? PreferredAgeMonthsMin,
    int? PreferredAgeMonthsMax,
    AnimalSize? PreferredSize,
    string? PreferredLocation,
    string? ExperienceWithPets,
    HomeEnvironment? HomeEnvironment,
    string? OtherPetsAtHome,
    string? ReasonForAdoption,
    ContactPreference ContactPreference,
    string? Description,
    AdoptionListingStatus Status,
    DateTime CreatedAt);

public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
