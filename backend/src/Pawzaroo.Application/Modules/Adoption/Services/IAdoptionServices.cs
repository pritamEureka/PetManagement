using Pawzaroo.Application.Modules.Adoption.Dtos;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Application.Modules.Adoption.Services;

public enum AdoptionListingScope { Public, Mine, Saved, AdminPending, AdminAll }

public record AdoptionListingQuery(
    AdoptionListingScope Scope = AdoptionListingScope.Public,
    string? Cursor = null,
    int PageSize = 20,
    AnimalType? AnimalType = null,
    string? Breed = null,
    AnimalSize? Size = null,
    Gender? Gender = null,
    string? Location = null,
    decimal? MaxFee = null,
    bool? VaccinatedOnly = null,
    bool? NeuteredOnly = null,
    bool? GoodWithChildren = null,
    bool? GoodWithOtherPets = null,
    AdoptionListingStatus? Status = null,
    string? Sort = null);

public interface IAdoptionListingService
{
    Task<CursorPage<AdoptionListingSummaryDto>> SearchAsync(AdoptionListingQuery query, CancellationToken ct = default);
    Task<AdoptionListingDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CreateAsync(CreateAdoptionListingInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAdoptionListingInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Owner draft -> PendingApproval. No-op if already submitted.</summary>
    Task SubmitForApprovalAsync(Guid id, CancellationToken ct = default);

    /// <summary>Owner closes a listing (e.g. before approval, or after).</summary>
    Task CloseAsync(Guid id, CancellationToken ct = default);

    /// <summary>Owner marks adopted (Approved -> Adopted), optionally captures adopter.</summary>
    Task MarkAdoptedAsync(Guid id, Guid? adoptedByUserId, CancellationToken ct = default);

    Task ApproveAsync(Guid id, string? adminNotes, CancellationToken ct = default);
    Task RejectAsync(Guid id, string? adminNotes, CancellationToken ct = default);
}

public interface IAdoptionRequestService
{
    Task<AdoptionRequestDto> CreateAsync(Guid listingId, CreateAdoptionRequestInput input, CancellationToken ct = default);
    Task<IReadOnlyList<AdoptionRequestDto>> ListForListingAsync(Guid listingId, CancellationToken ct = default);
    Task<IReadOnlyList<AdoptionRequestDto>> ListMineAsync(CancellationToken ct = default);
    Task WithdrawAsync(Guid requestId, CancellationToken ct = default);
    Task SetStatusAsync(Guid requestId, AdoptionRequestStatus status, CancellationToken ct = default);
}

public interface ISavedAdoptionService
{
    /// <summary>Returns the new saved state.</summary>
    Task<bool> ToggleAsync(Guid listingId, CancellationToken ct = default);
    Task<CursorPage<AdoptionListingSummaryDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);
}

public interface IAdoptionWantedPostService
{
    Task<Guid> CreateAsync(CreateWantedPostInput input, CancellationToken ct = default);
    Task<CursorPage<WantedPostDto>> SearchAsync(string? cursor, int pageSize, AnimalType? animalType, string? location, CancellationToken ct = default);
    Task<IReadOnlyList<WantedPostDto>> ListMineAsync(CancellationToken ct = default);
}
