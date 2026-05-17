using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Adoption.Dtos;
using Pawzaroo.Application.Modules.Adoption.Events;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

public class AdoptionRequestService : IAdoptionRequestService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public AdoptionRequestService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<AdoptionRequestDto> CreateAsync(Guid listingId, CreateAdoptionRequestInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.SingleOrDefaultAsync(l => l.Id == listingId, ct)
            ?? throw new NotFoundException("AdoptionListing", listingId);
        if (listing.OwnerId == uid) throw new ConflictException("You cannot request your own listing.");
        if (listing.Status != AdoptionListingStatus.Approved)
            throw new ConflictException("Listing is not open for requests.");

        if (string.IsNullOrWhiteSpace(input.Message))
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["message"] = new[] { "Required" } });

        if (await _db.AdoptionRequests.AnyAsync(r => r.AdoptionListingId == listingId && r.RequesterId == uid
                                                   && r.Status == AdoptionRequestStatus.Pending, ct))
            throw new ConflictException("You already have a pending request on this listing.");

        var req = new AdoptionRequest
        {
            AdoptionListingId = listingId,
            RequesterId = uid,
            Message = input.Message.Trim()
        };
        _db.AdoptionRequests.Add(req);

        _db.Notifications.Add(new InAppNotification
        {
            UserId = listing.OwnerId,
            Title = "New adoption interest",
            Body = $"Someone is interested in {listing.PetName ?? listing.Title}.",
            Url = $"/adoption/{listing.Id}"
        });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionRequestCreated(req.Id, listingId, uid, DateTime.UtcNow), req.Id.ToString(), ct);

        var requesterName = await _db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).SingleAsync(ct);
        return new AdoptionRequestDto(req.Id, listingId, uid, requesterName, req.Message, req.Status, req.CreatedAt);
    }

    public async Task<IReadOnlyList<AdoptionRequestDto>> ListForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var listing = await _db.AdoptionListings.AsNoTracking()
            .Where(l => l.Id == listingId).Select(l => new { l.OwnerId }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("AdoptionListing", listingId);
        if (listing.OwnerId != uid) throw new ForbiddenException();

        return await _db.AdoptionRequests.AsNoTracking()
            .Where(r => r.AdoptionListingId == listingId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdoptionRequestDto(r.Id, r.AdoptionListingId, r.RequesterId,
                r.Requester.DisplayName, r.Message, r.Status, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdoptionRequestDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.AdoptionRequests.AsNoTracking()
            .Where(r => r.RequesterId == uid)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdoptionRequestDto(r.Id, r.AdoptionListingId, r.RequesterId,
                r.Requester.DisplayName, r.Message, r.Status, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task WithdrawAsync(Guid requestId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var r = await _db.AdoptionRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("AdoptionRequest", requestId);
        if (r.RequesterId != uid) throw new ForbiddenException();
        r.Status = AdoptionRequestStatus.Withdrawn;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionRequestStatusChanged(r.Id, r.AdoptionListingId, r.Status.ToString(), DateTime.UtcNow),
            r.Id.ToString(), ct);
    }

    public async Task SetStatusAsync(Guid requestId, AdoptionRequestStatus status, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var r = await _db.AdoptionRequests.Include(x => x.AdoptionListing)
            .SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("AdoptionRequest", requestId);
        if (r.AdoptionListing.OwnerId != uid) throw new ForbiddenException();

        r.Status = status;
        _db.Notifications.Add(new InAppNotification
        {
            UserId = r.RequesterId,
            Title = "Adoption request update",
            Body = $"Your request status is now: {status}.",
            Url = $"/adoption/{r.AdoptionListingId}"
        });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionRequestStatusChanged(r.Id, r.AdoptionListingId, status.ToString(), DateTime.UtcNow),
            r.Id.ToString(), ct);
    }
}

public class SavedAdoptionService : ISavedAdoptionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAdoptionListingService _listings;

    public SavedAdoptionService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IAdoptionListingService listings)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _listings = listings;
    }

    public async Task<bool> ToggleAsync(Guid listingId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var existing = await _db.SavedAdoptionListings
            .SingleOrDefaultAsync(s => s.UserId == uid && s.AdoptionListingId == listingId, ct);
        bool saved;
        if (existing is null)
        {
            if (!await _db.AdoptionListings.AnyAsync(l => l.Id == listingId, ct))
                throw new NotFoundException("AdoptionListing", listingId);
            _db.SavedAdoptionListings.Add(new SavedAdoptionListing { UserId = uid, AdoptionListingId = listingId });
            saved = true;
        }
        else
        {
            _db.SavedAdoptionListings.Remove(existing);
            saved = false;
        }
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionListingSaved(listingId, uid, saved, DateTime.UtcNow), listingId.ToString(), ct);
        return saved;
    }

    public Task<CursorPage<AdoptionListingSummaryDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default)
        => _listings.SearchAsync(new AdoptionListingQuery(AdoptionListingScope.Saved, cursor, pageSize), ct);
}

public class AdoptionWantedPostService : IAdoptionWantedPostService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public AdoptionWantedPostService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<Guid> CreateAsync(CreateWantedPostInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var p = new AdoptionWantedPost
        {
            RequesterId = uid,
            AnimalType = input.AnimalType,
            Breed = input.Breed,
            PreferredAgeMonthsMin = input.PreferredAgeMonthsMin,
            PreferredAgeMonthsMax = input.PreferredAgeMonthsMax,
            PreferredSize = input.PreferredSize,
            PreferredLocation = input.PreferredLocation,
            ExperienceWithPets = input.ExperienceWithPets,
            HomeEnvironment = input.HomeEnvironment,
            OtherPetsAtHome = input.OtherPetsAtHome,
            ReasonForAdoption = input.ReasonForAdoption,
            ContactPreference = input.ContactPreference,
            Description = input.Description,
            Status = AdoptionListingStatus.Approved // wanted posts auto-approve in MVP
        };
        _db.AdoptionWantedPosts.Add(p);
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(AdoptionTopics.Events,
            new AdoptionWantedPostCreated(p.Id, uid, DateTime.UtcNow), p.Id.ToString(), ct);
        return p.Id;
    }

    public async Task<CursorPage<WantedPostDto>> SearchAsync(string? cursor, int pageSize, AnimalType? animalType, string? location, CancellationToken ct = default)
    {
        var q = _db.AdoptionWantedPosts.AsNoTracking().Where(p => p.Status == AdoptionListingStatus.Approved);
        if (animalType.HasValue) q = q.Where(p => p.AnimalType == animalType);
        if (!string.IsNullOrWhiteSpace(location))
            q = q.Where(p => p.PreferredLocation != null && p.PreferredLocation.Contains(location));

        var cur = AdoptionCursor.Decode(cursor);
        if (cur is { } c)
            q = q.Where(p => p.CreatedAt < c.CreatedAt || (p.CreatedAt == c.CreatedAt && p.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(pageSize, 1, 50);
        var rows = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(take + 1)
            .Select(p => new WantedPostDto(
                p.Id, p.RequesterId, p.Requester.DisplayName, p.AnimalType, p.Breed,
                p.PreferredAgeMonthsMin, p.PreferredAgeMonthsMax, p.PreferredSize, p.PreferredLocation,
                p.ExperienceWithPets, p.HomeEnvironment, p.OtherPetsAtHome,
                p.ReasonForAdoption, p.ContactPreference, p.Description, p.Status, p.CreatedAt))
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = AdoptionCursor.Encode(last.CreatedAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }
        return new CursorPage<WantedPostDto>(rows, next);
    }

    public async Task<IReadOnlyList<WantedPostDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.AdoptionWantedPosts.AsNoTracking()
            .Where(p => p.RequesterId == uid)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new WantedPostDto(
                p.Id, p.RequesterId, p.Requester.DisplayName, p.AnimalType, p.Breed,
                p.PreferredAgeMonthsMin, p.PreferredAgeMonthsMax, p.PreferredSize, p.PreferredLocation,
                p.ExperienceWithPets, p.HomeEnvironment, p.OtherPetsAtHome,
                p.ReasonForAdoption, p.ContactPreference, p.Description, p.Status, p.CreatedAt))
            .ToListAsync(ct);
    }
}
