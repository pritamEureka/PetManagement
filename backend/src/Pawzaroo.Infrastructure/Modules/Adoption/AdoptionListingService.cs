using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Adoption.Dtos;
using Pawzaroo.Application.Modules.Adoption.Events;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

public class AdoptionListingService : IAdoptionListingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly IRedisCacheService _cache;

    private const string FirstPageCacheKey = "adoption:first-page";

    public AdoptionListingService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit, IRedisCacheService cache)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _cache = cache;
    }

    private bool IsAdmin =>
        _current.Permissions.Contains(Permissions.Adoption.Approve)
        || _current.Permissions.Contains(Permissions.Adoption.Reject);

    public async Task<CursorPage<AdoptionListingSummaryDto>> SearchAsync(AdoptionListingQuery q, CancellationToken ct = default)
    {
        var uid = _current.UserId;

        // Hot first-page cache for the default public scope only.
        bool isHotPath = q.Scope == AdoptionListingScope.Public && q.Cursor is null
                         && q.AnimalType is null && q.Breed is null && q.Size is null && q.Gender is null
                         && q.Location is null && q.MaxFee is null
                         && q.VaccinatedOnly is null && q.NeuteredOnly is null
                         && q.GoodWithChildren is null && q.GoodWithOtherPets is null && q.Status is null;
        if (isHotPath)
        {
            var cached = await _cache.GetAsync<CursorPage<AdoptionListingSummaryDto>>(FirstPageCacheKey, ct);
            if (cached is not null) return cached;
        }

        var query = _db.AdoptionListings.AsNoTracking().AsQueryable();

        switch (q.Scope)
        {
            case AdoptionListingScope.Public:
                query = query.Where(l => l.Status == AdoptionListingStatus.Approved);
                break;
            case AdoptionListingScope.Mine:
                if (uid is null) throw new ForbiddenException();
                query = query.Where(l => l.OwnerId == uid);
                break;
            case AdoptionListingScope.Saved:
                if (uid is null) throw new ForbiddenException();
                query = query.Where(l => _db.SavedAdoptionListings.Any(s => s.UserId == uid && s.AdoptionListingId == l.Id));
                break;
            case AdoptionListingScope.AdminPending:
                if (!IsAdmin) throw new ForbiddenException();
                query = query.Where(l => l.Status == AdoptionListingStatus.PendingApproval);
                break;
            case AdoptionListingScope.AdminAll:
                if (!IsAdmin) throw new ForbiddenException();
                break;
        }

        if (q.AnimalType.HasValue)      query = query.Where(l => l.AnimalType == q.AnimalType);
        if (!string.IsNullOrWhiteSpace(q.Breed))    query = query.Where(l => l.Breed != null && l.Breed.Contains(q.Breed));
        if (q.Size.HasValue)            query = query.Where(l => l.Size == q.Size);
        if (q.Gender.HasValue)          query = query.Where(l => l.Gender == q.Gender);
        if (!string.IsNullOrWhiteSpace(q.Location)) query = query.Where(l => l.Location != null && l.Location.Contains(q.Location));
        if (q.MaxFee.HasValue)          query = query.Where(l => l.AdoptionFee <= q.MaxFee);
        if (q.VaccinatedOnly == true)   query = query.Where(l => l.Vaccinated);
        if (q.NeuteredOnly == true)     query = query.Where(l => l.NeuteredSpayed);
        if (q.GoodWithChildren == true) query = query.Where(l => l.GoodWithChildren == true);
        if (q.GoodWithOtherPets == true) query = query.Where(l => l.GoodWithOtherPets == true);
        if (q.Status.HasValue && q.Scope is AdoptionListingScope.AdminAll or AdoptionListingScope.Mine)
            query = query.Where(l => l.Status == q.Status);

        var cur = AdoptionCursor.Decode(q.Cursor);
        if (cur is { } c)
            query = query.Where(l => l.CreatedAt < c.CreatedAt || (l.CreatedAt == c.CreatedAt && l.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(q.PageSize, 1, 50);
        var rows = await query
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Take(take + 1)
            .Select(l => new
            {
                l.Id, l.Title, l.PetName, l.AnimalType, l.Breed, l.Gender, l.Size,
                l.AgeMonths, l.Location, l.AdoptionFee, l.Vaccinated, l.NeuteredSpayed,
                Photos = l.Photos.OrderBy(p => p.OrderIndex).Select(p => p.Url).ToList(),
                l.OwnerId, OwnerName = l.Owner.DisplayName, OwnerAvatar = l.Owner.AvatarUrl,
                l.Status, l.CreatedAt,
                IsSaved = uid != null && _db.SavedAdoptionListings.Any(s => s.UserId == uid && s.AdoptionListingId == l.Id),
                IsOwn = uid != null && l.OwnerId == uid
            })
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = AdoptionCursor.Encode(last.CreatedAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }

        var items = rows.Select(r => new AdoptionListingSummaryDto(
            r.Id, r.Title, r.PetName, r.AnimalType, r.Breed, r.Gender, r.Size, r.AgeMonths,
            r.Location, r.AdoptionFee, r.Vaccinated, r.NeuteredSpayed, r.Photos,
            r.OwnerId, r.OwnerName, r.OwnerAvatar, r.Status, r.IsSaved, r.IsOwn, r.CreatedAt
        )).ToList();

        var page = new CursorPage<AdoptionListingSummaryDto>(items, next);
        if (isHotPath) await _cache.SetAsync(FirstPageCacheKey, page, TimeSpan.FromMinutes(2), ct);
        return page;
    }

    public async Task<AdoptionListingDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        return await _db.AdoptionListings.AsNoTracking().Where(l => l.Id == id)
            .Select(l => new AdoptionListingDetailDto(
                l.Id, l.Title, l.PetName, l.Description, l.AnimalType, l.Breed, l.AgeMonths,
                l.Gender, l.Size, l.Color, l.Vaccinated, l.VaccinationDetails, l.NeuteredSpayed,
                l.HealthCondition, l.BehaviorNotes, l.GoodWithChildren, l.GoodWithOtherPets,
                l.Location, l.AdoptionFee, l.ReasonForListing, l.ContactPreference,
                l.Status, l.AdminNotes, l.SubmittedAt, l.DecidedAt, l.AdoptedAt,
                l.OwnerId, l.Owner.DisplayName, l.Owner.AvatarUrl,
                l.Photos.OrderBy(p => p.OrderIndex).Select(p => p.Url).ToList(),
                uid != null && _db.SavedAdoptionListings.Any(s => s.UserId == uid && s.AdoptionListingId == l.Id),
                uid != null && l.OwnerId == uid,
                l.Requests.Count,
                l.CreatedAt))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(CreateAdoptionListingInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = MapNew(input, uid);
        if (input.SubmitForApproval)
        {
            listing.Status = AdoptionListingStatus.PendingApproval;
            listing.SubmittedAt = DateTime.UtcNow;
        }

        _db.AdoptionListings.Add(listing);

        if (input.SubmitForApproval)
        {
            _db.ApprovalRequests.Add(new ApprovalRequest
            {
                EntityType = ApprovalEntityType.AdoptionListing,
                EntityId = listing.Id,
                SubmittedById = uid,
                Decision = ApprovalDecision.Pending,
                SlaDueAt = DateTime.UtcNow.AddHours(48),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { listing.Title, listing.AnimalType })
            });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingCreated(listing.Id, uid, input.SubmitForApproval, DateTime.UtcNow),
            listing.Id.ToString(), ct);
        if (input.SubmitForApproval)
            await _kafka.PublishAsync(AdoptionTopics.Approvals,
                new AdoptionListingSubmitted(listing.Id, uid, DateTime.UtcNow), listing.Id.ToString(), ct);
        return listing.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateAdoptionListingInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.Include(l => l.Photos)
            .SingleOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("AdoptionListing", id);
        if (listing.OwnerId != uid && !IsAdmin) throw new ForbiddenException();

        listing.Title = input.Title;
        listing.PetName = input.PetName;
        listing.Description = input.Description;
        listing.AnimalType = input.AnimalType;
        listing.Breed = input.Breed;
        listing.AgeMonths = input.AgeMonths;
        listing.Gender = input.Gender;
        listing.Size = input.Size;
        listing.Color = input.Color;
        listing.Vaccinated = input.Vaccinated;
        listing.VaccinationDetails = input.VaccinationDetails;
        listing.NeuteredSpayed = input.NeuteredSpayed;
        listing.HealthCondition = input.HealthCondition;
        listing.BehaviorNotes = input.BehaviorNotes;
        listing.GoodWithChildren = input.GoodWithChildren;
        listing.GoodWithOtherPets = input.GoodWithOtherPets;
        listing.Location = input.Location;
        listing.AdoptionFee = input.AdoptionFee;
        listing.ReasonForListing = input.ReasonForListing;
        listing.ContactPreference = input.ContactPreference;

        if (input.PhotoUrls is not null)
        {
            _db.AdoptionListingPhotos.RemoveRange(listing.Photos);
            listing.Photos.Clear();
            for (int i = 0; i < input.PhotoUrls.Count; i++)
                listing.Photos.Add(new AdoptionListingPhoto { Url = input.PhotoUrls[i], OrderIndex = i });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingUpdated(listing.Id, listing.OwnerId, DateTime.UtcNow),
            listing.Id.ToString(), ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("AdoptionListing", id);
        if (listing.OwnerId != uid && !IsAdmin) throw new ForbiddenException();
        _db.AdoptionListings.Remove(listing);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingDeleted(listing.Id, listing.OwnerId, DateTime.UtcNow),
            listing.Id.ToString(), ct);
    }

    public async Task SubmitForApprovalAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("AdoptionListing", id);
        if (listing.OwnerId != uid) throw new ForbiddenException();
        if (listing.Status is not (AdoptionListingStatus.Draft or AdoptionListingStatus.Rejected))
            throw new ConflictException($"Cannot submit from status {listing.Status}.");

        listing.Status = AdoptionListingStatus.PendingApproval;
        listing.SubmittedAt = DateTime.UtcNow;

        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            EntityType = ApprovalEntityType.AdoptionListing,
            EntityId = listing.Id,
            SubmittedById = uid,
            Decision = ApprovalDecision.Pending,
            SlaDueAt = DateTime.UtcNow.AddHours(48)
        });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Approvals,
            new AdoptionListingSubmitted(listing.Id, uid, DateTime.UtcNow), listing.Id.ToString(), ct);
    }

    public async Task CloseAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("AdoptionListing", id);
        if (listing.OwnerId != uid && !IsAdmin) throw new ForbiddenException();
        listing.Status = AdoptionListingStatus.Closed;
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingClosed(listing.Id, listing.OwnerId, DateTime.UtcNow),
            listing.Id.ToString(), ct);
    }

    public async Task MarkAdoptedAsync(Guid id, Guid? adoptedByUserId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("AdoptionListing", id);
        if (listing.OwnerId != uid && !IsAdmin) throw new ForbiddenException();
        if (listing.Status is AdoptionListingStatus.Adopted or AdoptionListingStatus.Closed)
            throw new ConflictException("Listing already finalized.");

        listing.Status = AdoptionListingStatus.Adopted;
        listing.AdoptedAt = DateTime.UtcNow;
        listing.AdoptedByUserId = adoptedByUserId;
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _audit.LogAsync("mark_adopted", "AdoptionListing", listing.Id.ToString(), "Adoption",
            newValues: new { adoptedByUserId }, ct: ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingAdopted(listing.Id, listing.OwnerId, adoptedByUserId, DateTime.UtcNow),
            listing.Id.ToString(), ct);
    }

    public Task ApproveAsync(Guid id, string? adminNotes, CancellationToken ct = default)
        => DecideAsync(id, AdoptionListingStatus.Approved, adminNotes, ct);

    public Task RejectAsync(Guid id, string? adminNotes, CancellationToken ct = default)
        => DecideAsync(id, AdoptionListingStatus.Rejected, adminNotes, ct);

    private async Task DecideAsync(Guid id, AdoptionListingStatus status, string? adminNotes, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!IsAdmin) throw new ForbiddenException();

        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("AdoptionListing", id);

        listing.Status = status;
        listing.AdminNotes = adminNotes;
        listing.DecidedAt = DateTime.UtcNow;
        listing.DecidedByUserId = uid;

        var ar = await _db.ApprovalRequests.FirstOrDefaultAsync(
            a => a.EntityType == ApprovalEntityType.AdoptionListing && a.EntityId == id
                 && a.Decision == ApprovalDecision.Pending, ct);
        if (ar is not null)
        {
            ar.Decision = status == AdoptionListingStatus.Approved
                ? ApprovalDecision.Approved
                : ApprovalDecision.Rejected;
            ar.DecidedById = uid;
            ar.DecidedAt = DateTime.UtcNow;
            ar.AdminNotes = adminNotes;
        }

        // Notify owner (in-app).
        _db.Notifications.Add(new InAppNotification
        {
            UserId = listing.OwnerId,
            Title = status == AdoptionListingStatus.Approved
                ? "Your adoption listing was approved 🎉"
                : "Your adoption listing was rejected",
            Body = adminNotes ?? listing.Title,
            Url = $"/adoption/{listing.Id}"
        });

        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(FirstPageCacheKey, ct);
        await _audit.LogAsync(status.ToString().ToLowerInvariant(), "AdoptionListing", listing.Id.ToString(), "Adoption",
            newValues: new { adminNotes }, ct: ct);

        if (status == AdoptionListingStatus.Approved)
            await _kafka.PublishAsync(AdoptionTopics.Approvals,
                new AdoptionListingApproved(id, uid, DateTime.UtcNow), id.ToString(), ct);
        else
            await _kafka.PublishAsync(AdoptionTopics.Approvals,
                new AdoptionListingRejected(id, uid, adminNotes, DateTime.UtcNow), id.ToString(), ct);
    }

    private static AdoptionListing MapNew(CreateAdoptionListingInput input, Guid ownerId)
    {
        var listing = new AdoptionListing
        {
            OwnerId = ownerId,
            PetId = input.PetId,
            Title = input.Title,
            PetName = input.PetName,
            Description = input.Description,
            AnimalType = input.AnimalType,
            Breed = input.Breed,
            AgeMonths = input.AgeMonths,
            Gender = input.Gender,
            Size = input.Size,
            Color = input.Color,
            Vaccinated = input.Vaccinated,
            VaccinationDetails = input.VaccinationDetails,
            NeuteredSpayed = input.NeuteredSpayed,
            HealthCondition = input.HealthCondition,
            BehaviorNotes = input.BehaviorNotes,
            GoodWithChildren = input.GoodWithChildren,
            GoodWithOtherPets = input.GoodWithOtherPets,
            Location = input.Location,
            AdoptionFee = input.AdoptionFee,
            ReasonForListing = input.ReasonForListing,
            ContactPreference = input.ContactPreference,
            Status = AdoptionListingStatus.Draft
        };
        if (input.PhotoUrls is { Count: > 0 })
            for (int i = 0; i < input.PhotoUrls.Count; i++)
                listing.Photos.Add(new AdoptionListingPhoto { Url = input.PhotoUrls[i], OrderIndex = i });
        return listing;
    }
}
