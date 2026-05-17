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
    DateTime CreatedAt);

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
    DateTime CreatedAt);

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
    bool SubmitForApproval);

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
    IReadOnlyList<string>? PhotoUrls);

public record CreateAdoptionRequestInput(string Message);

public record MarkAdoptedInput(Guid? AdoptedByUserId);

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
