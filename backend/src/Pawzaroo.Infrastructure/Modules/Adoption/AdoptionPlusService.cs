using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

// ---------------- DTOs ----------------
public record StageEventDto(AdoptionApplicationStage Stage, DateTime EnteredAt, Guid? ByUserId, string? Note, DateTime? DueAt);
public record PipelineItemDto(Guid RequestId, Guid ListingId, string ListingTitle, Guid RequesterId, string RequesterName,
    string Message, AdoptionRequestStatus Status, AdoptionApplicationStage Stage, DateTime StageEnteredAt, DateTime? DueAt,
    bool IsOverdue, int? MatchPercent, DateTime CreatedAt);
public record MyApplicationDto(Guid RequestId, Guid ListingId, string ListingTitle, AdoptionRequestStatus Status,
    AdoptionApplicationStage Stage, DateTime StageEnteredAt, DateTime? DueAt, IReadOnlyList<StageEventDto> History);
public record ReferenceDto(Guid Id, Guid RequestId, string Name, string Contact, string? Relationship, AdoptionReferenceStatus Status, string? Notes);
public record HomeVisitDto(Guid Id, Guid RequestId, DateTime ScheduledAt, AdoptionHomeVisitStatus Status, string? Notes, string? Outcome);
public record FollowUpDto(Guid Id, Guid ListingId, string ListingTitle, int DayOffset, DateTime DueAt, AdoptionFollowUpStatus Status,
    string? Notes, int? WelfareScore, DateTime? CompletedAt);
public record FollowUpSummaryDto(int Total, int Completed, double? AverageWelfare, int Concerning);
public record FosterDto(Guid Id, Guid ListingId, Guid? PetId, Guid FosterUserId, string FosterName, DateTime StartedAt, DateTime? EndedAt, FosterOutcome Outcome, string? Notes);
public record AdopterProfileDto(int HouseholdSize, string? ChildrenAges, string? ExistingPetsJson, AdopterExperience Experience,
    HomeEnvironment? HomeType, bool HasYard, int TravelDistanceKm, ActivityLevel Activity, double? Latitude, double? Longitude);
public record MatchDto(Guid ListingId, int Percent, IReadOnlyList<string> Explanations);
public record RiskFlagDto(Guid ListingId, ListingRiskKind Kind, string Detail, int Severity, DateTime CreatedAt);

public interface IAdoptionPlusService
{
    Task<IReadOnlyList<PipelineItemDto>> GetPipelineAsync(Guid? listingId, CancellationToken ct = default);
    Task<IReadOnlyList<MyApplicationDto>> GetMyApplicationsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StageEventDto>> GetTimelineAsync(Guid requestId, CancellationToken ct = default);
    Task AdvanceAsync(Guid requestId, AdoptionApplicationStage stage, string? note, CancellationToken ct = default);

    Task<IReadOnlyList<ReferenceDto>> ListReferencesAsync(Guid requestId, CancellationToken ct = default);
    Task<ReferenceDto> AddReferenceAsync(Guid requestId, string name, string contact, string? relationship, CancellationToken ct = default);
    Task<ReferenceDto> UpdateReferenceAsync(Guid referenceId, AdoptionReferenceStatus status, string? notes, CancellationToken ct = default);
    Task<IReadOnlyList<HomeVisitDto>> ListHomeVisitsAsync(Guid requestId, CancellationToken ct = default);
    Task<HomeVisitDto> ScheduleHomeVisitAsync(Guid requestId, DateTime scheduledAt, string? notes, CancellationToken ct = default);
    Task<HomeVisitDto> UpdateHomeVisitAsync(Guid visitId, AdoptionHomeVisitStatus status, string? notes, string? outcome, CancellationToken ct = default);

    Task<IReadOnlyList<FollowUpDto>> ListMyFollowUpsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FollowUpDto>> ListFollowUpsForListingAsync(Guid listingId, CancellationToken ct = default);
    Task CompleteFollowUpAsync(Guid followUpId, int welfareScore, string? notes, CancellationToken ct = default);
    Task<FollowUpSummaryDto> GetOutcomeSummaryAsync(Guid listingId, CancellationToken ct = default);

    Task<IReadOnlyList<FosterDto>> ListFostersAsync(Guid listingId, CancellationToken ct = default);
    Task<FosterDto> StartFosterAsync(Guid listingId, Guid fosterUserId, string? notes, CancellationToken ct = default);
    Task EndFosterAsync(Guid placementId, FosterOutcome outcome, string? notes, CancellationToken ct = default);

    Task<AdopterProfileDto?> GetProfileAsync(CancellationToken ct = default);
    Task<AdopterProfileDto> UpsertProfileAsync(AdopterProfileDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<MatchDto>> MatchListingsAsync(IReadOnlyList<Guid> listingIds, CancellationToken ct = default);

    Task<IReadOnlyList<RiskFlagDto>> GetRiskFlagsAsync(IReadOnlyList<Guid> listingIds, CancellationToken ct = default);
}

/// <summary>Helpers shared with the listing/request services and the SLA job. All no-save: the caller commits.</summary>
public static class AdoptionStageRecorder
{
    public static void Record(ApplicationDbContext db, Guid requestId, AdoptionApplicationStage stage, Guid? by, string? note = null, DateTime? dueAt = null)
        => db.Set<AdoptionRequestStageEvent>().Add(new AdoptionRequestStageEvent
        { RequestId = requestId, Stage = stage, ByUserId = by, Note = note, DueAt = dueAt });

    public static AdoptionApplicationStage? MapLegacyStatus(AdoptionRequestStatus s) => s switch
    {
        AdoptionRequestStatus.Approved or AdoptionRequestStatus.Selected => AdoptionApplicationStage.Decision,
        AdoptionRequestStatus.Rejected => AdoptionApplicationStage.Declined,
        AdoptionRequestStatus.Withdrawn => AdoptionApplicationStage.Withdrawn,
        _ => null
    };
}

public static class AdoptionPlusHooks
{
    /// <summary>Called by the existing mark-adopted flow after it saved. Never throws into the caller.</summary>
    public static async Task OnAdoptedAsync(ApplicationDbContext db, AdoptionListing listing, Guid? adopterId, CancellationToken ct)
    {
        try
        {
            if (adopterId is not { } adopter) return;
            var now = listing.AdoptedAt ?? DateTime.UtcNow;
            var existing = await db.Set<AdoptionFollowUp>().Where(f => f.ListingId == listing.Id).Select(f => f.DayOffset).ToListAsync(ct);
            foreach (var (offset, due) in AdoptionSla.FollowUpSchedule(now).Where(s => !existing.Contains(s.Offset)))
                db.Set<AdoptionFollowUp>().Add(new AdoptionFollowUp { ListingId = listing.Id, AdopterId = adopter, DayOffset = offset, DueAt = due });

            var reqs = await db.AdoptionRequests.Where(r => r.AdoptionListingId == listing.Id && r.RequesterId == adopter).Select(r => r.Id).ToListAsync(ct);
            foreach (var rid in reqs)
            {
                var last = await db.Set<AdoptionRequestStageEvent>().Where(e => e.RequestId == rid)
                    .OrderByDescending(e => e.EnteredAt).Select(e => (AdoptionApplicationStage?)e.Stage).FirstOrDefaultAsync(ct);
                if (last is not (AdoptionApplicationStage.Adopted or AdoptionApplicationStage.FollowUp))
                    AdoptionStageRecorder.Record(db, rid, AdoptionApplicationStage.Adopted, listing.OwnerId, "Adoption confirmed");
            }

            db.Notifications.Add(new InAppNotification
            {
                UserId = adopter,
                Title = "Adoption confirmed",
                Body = AdoptionStageRules.NotificationBody(AdoptionApplicationStage.Adopted, listing.PetName ?? listing.Title)
                       + " We will check in after 1 week, 1 month and 3 months.",
                Url = "/adoption/follow-ups"
            });
            await db.SaveChangesAsync(ct);
        }
        catch { /* best effort: adoption itself already committed */ }
    }
}

public static class ListingRiskEvaluator
{
    /// <summary>Recomputes risk flags for a listing. Flags only inform admins — never auto-reject. Best effort.</summary>
    public static async Task EvaluateAndStoreAsync(ApplicationDbContext db, Guid listingId, CancellationToken ct)
    {
        try
        {
            var l = await db.AdoptionListings.AsNoTracking().Where(x => x.Id == listingId)
                .Select(x => new { x.Id, x.OwnerId, x.Title, x.AdoptionFee, OwnerCreated = x.Owner.CreatedAt, x.Owner.PhoneNumber })
                .SingleOrDefaultAsync(ct);
            if (l is null) return;
            var photos = await db.AdoptionListingPhotos.AsNoTracking().Where(p => p.AdoptionListingId == listingId).Select(p => p.Url).ToListAsync(ct);
            var ownerCount = await db.AdoptionListings.CountAsync(x => x.OwnerId == l.OwnerId, ct);
            var since = DateTime.UtcNow.AddDays(-ListingRiskRules.WindowDays);
            var raw = await db.AdoptionListings.AsNoTracking()
                .Where(x => x.Id != listingId && x.OwnerId != l.OwnerId && x.CreatedAt >= since)
                .Select(x => new { x.OwnerId, x.Title, x.CreatedAt, x.Owner.PhoneNumber, Photos = x.Photos.Select(p => p.Url).ToList() })
                .Take(2000).ToListAsync(ct);
            var others = raw.Select(x => new RiskOther(x.OwnerId, x.Title, x.Photos, x.PhoneNumber, x.CreatedAt)).ToList();

            var flags = ListingRiskRules.Evaluate(
                new RiskCandidate(l.Id, l.OwnerId, l.Title, l.AdoptionFee, photos, l.PhoneNumber, l.OwnerCreated, ownerCount),
                others, DateTime.UtcNow);

            var old = await db.Set<AdoptionListingRiskFlag>().Where(f => f.ListingId == listingId).ToListAsync(ct);
            db.Set<AdoptionListingRiskFlag>().RemoveRange(old);
            foreach (var f in flags)
                db.Set<AdoptionListingRiskFlag>().Add(new AdoptionListingRiskFlag { ListingId = listingId, Kind = f.Kind, Detail = f.Detail, Severity = f.Severity });
            await db.SaveChangesAsync(ct);
        }
        catch { /* risk scoring must never block listing creation */ }
    }
}

public class AdoptionPlusService : IAdoptionPlusService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAdoptionListingService _listings;
    private readonly IConfiguration _config;

    public AdoptionPlusService(ApplicationDbContext db, ICurrentUserService current, IAdoptionListingService listings, IConfiguration config)
    { _db = db; _current = current; _listings = listings; _config = config; }

    private Guid Uid => _current.UserId ?? throw new ForbiddenException();
    private int SlaDays => AdoptionSla.NormalizeDays(_config.GetValue<int?>("Adoption:DecisionSlaDays"));

    private async Task<AdoptionRequest> RequestAsOwnerAsync(Guid requestId, CancellationToken ct)
    {
        var uid = Uid;
        var r = await _db.AdoptionRequests.Include(x => x.AdoptionListing).SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("AdoptionRequest", requestId);
        if (r.AdoptionListing.OwnerId != uid) throw new ForbiddenException();
        return r;
    }

    private async Task<AdoptionRequest> RequestAsParticipantAsync(Guid requestId, CancellationToken ct)
    {
        var uid = Uid;
        var r = await _db.AdoptionRequests.Include(x => x.AdoptionListing).SingleOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new NotFoundException("AdoptionRequest", requestId);
        if (r.AdoptionListing.OwnerId != uid && r.RequesterId != uid) throw new ForbiddenException();
        return r;
    }

    private async Task<AdoptionApplicationStage> CurrentStageAsync(Guid requestId, CancellationToken ct)
        => await _db.Set<AdoptionRequestStageEvent>().Where(e => e.RequestId == requestId)
            .OrderByDescending(e => e.EnteredAt).Select(e => (AdoptionApplicationStage?)e.Stage).FirstOrDefaultAsync(ct)
           ?? AdoptionApplicationStage.Submitted;

    // ---------- Stages ----------

    public async Task<IReadOnlyList<PipelineItemDto>> GetPipelineAsync(Guid? listingId, CancellationToken ct = default)
    {
        var uid = Uid;
        var q = _db.AdoptionRequests.AsNoTracking().Where(r => r.AdoptionListing.OwnerId == uid);
        if (listingId is { } lid) q = q.Where(r => r.AdoptionListingId == lid);
        var rows = await q.OrderByDescending(r => r.CreatedAt).Take(500).Select(r => new
        {
            r.Id, r.AdoptionListingId, ListingTitle = r.AdoptionListing.Title, r.RequesterId, Name = r.Requester.DisplayName,
            r.Message, r.Status, r.CreatedAt
        }).ToListAsync(ct);

        var ids = rows.Select(r => r.Id).ToList();
        var events = await _db.Set<AdoptionRequestStageEvent>().AsNoTracking().Where(e => ids.Contains(e.RequestId)).ToListAsync(ct);
        var latest = events.GroupBy(e => e.RequestId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EnteredAt).First());

        var matches = await ComputeMatchesAsync(rows.Select(r => (r.RequesterId, r.AdoptionListingId)).ToList(), ct);
        var now = DateTime.UtcNow;
        return rows.Select(r =>
        {
            latest.TryGetValue(r.Id, out var ev);
            var stage = ev?.Stage ?? AdoptionApplicationStage.Submitted;
            return new PipelineItemDto(r.Id, r.AdoptionListingId, r.ListingTitle, r.RequesterId, r.Name, r.Message, r.Status,
                stage, ev?.EnteredAt ?? r.CreatedAt, ev?.DueAt,
                stage == AdoptionApplicationStage.Decision && AdoptionSla.IsOverdue(ev?.DueAt, now),
                matches.TryGetValue((r.RequesterId, r.AdoptionListingId), out var m) ? m.Percent : null, r.CreatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<MyApplicationDto>> GetMyApplicationsAsync(CancellationToken ct = default)
    {
        var uid = Uid;
        var rows = await _db.AdoptionRequests.AsNoTracking().Where(r => r.RequesterId == uid)
            .OrderByDescending(r => r.CreatedAt).Take(200)
            .Select(r => new { r.Id, r.AdoptionListingId, Title = r.AdoptionListing.Title, r.Status, r.CreatedAt }).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var events = await _db.Set<AdoptionRequestStageEvent>().AsNoTracking().Where(e => ids.Contains(e.RequestId)).ToListAsync(ct);
        return rows.Select(r =>
        {
            var hist = events.Where(e => e.RequestId == r.Id).OrderBy(e => e.EnteredAt)
                .Select(e => new StageEventDto(e.Stage, e.EnteredAt, e.ByUserId, e.Note, e.DueAt)).ToList();
            if (hist.Count == 0) hist.Add(new StageEventDto(AdoptionApplicationStage.Submitted, r.CreatedAt, null, null, null));
            var last = hist[^1];
            return new MyApplicationDto(r.Id, r.AdoptionListingId, r.Title, r.Status, last.Stage, last.EnteredAt, last.DueAt, hist);
        }).ToList();
    }

    public async Task<IReadOnlyList<StageEventDto>> GetTimelineAsync(Guid requestId, CancellationToken ct = default)
    {
        await RequestAsParticipantAsync(requestId, ct);
        return await _db.Set<AdoptionRequestStageEvent>().AsNoTracking().Where(e => e.RequestId == requestId)
            .OrderBy(e => e.EnteredAt).Select(e => new StageEventDto(e.Stage, e.EnteredAt, e.ByUserId, e.Note, e.DueAt)).ToListAsync(ct);
    }

    public async Task AdvanceAsync(Guid requestId, AdoptionApplicationStage stage, string? note, CancellationToken ct = default)
    {
        var uid = Uid;
        var r = await RequestAsOwnerAsync(requestId, ct);
        if (r.Status == AdoptionRequestStatus.Withdrawn) throw new ConflictException("Request was withdrawn.");
        var current = await CurrentStageAsync(requestId, ct);
        if (!AdoptionStageRules.CanTransition(current, stage))
            throw new ConflictException($"Cannot move an application from {current} to {stage}.");
        if (stage == AdoptionApplicationStage.Withdrawn) throw new ConflictException("Only the applicant can withdraw.");

        var label = r.AdoptionListing.PetName ?? r.AdoptionListing.Title;
        note = note?.Trim();
        if (note is { Length: > 2000 }) note = note[..2000];

        if (stage == AdoptionApplicationStage.Adopted)
        {
            // Feeds the existing mark-adopted flow (step-up/transfer rules unchanged: no pet transfer here).
            await _listings.MarkAdoptedAsync(r.AdoptionListingId, r.RequesterId, false, false, ct);
            return; // hook recorded Adopted stage, follow-ups and the notification
        }

        var due = stage == AdoptionApplicationStage.Decision ? AdoptionSla.DecisionDue(DateTime.UtcNow, SlaDays) : (DateTime?)null;
        AdoptionStageRecorder.Record(_db, requestId, stage, uid, note, due);

        // Keep legacy status coherent without changing its semantics.
        if (stage == AdoptionApplicationStage.Declined) r.Status = AdoptionRequestStatus.Rejected;
        else if (stage == AdoptionApplicationStage.Decision && r.Status == AdoptionRequestStatus.Pending) r.Status = AdoptionRequestStatus.Selected;

        _db.Notifications.Add(new InAppNotification
        {
            UserId = r.RequesterId,
            Title = stage == AdoptionApplicationStage.Declined ? "Adoption application update" : "Adoption application progress",
            Body = AdoptionStageRules.NotificationBody(stage, label) + (note is { Length: > 0 } ? $" Note: {note}" : ""),
            Url = "/adoption/my-applications"
        });
        await _db.SaveChangesAsync(ct);
    }

    // ---------- References / home visits ----------

    private static ReferenceDto ToDto(AdoptionReference x) => new(x.Id, x.RequestId, x.Name, x.Contact, x.Relationship, x.Status, x.Notes);
    private static HomeVisitDto ToDto(AdoptionHomeVisit x) => new(x.Id, x.RequestId, x.ScheduledAt, x.Status, x.Notes, x.Outcome);

    public async Task<IReadOnlyList<ReferenceDto>> ListReferencesAsync(Guid requestId, CancellationToken ct = default)
    {
        await RequestAsParticipantAsync(requestId, ct);
        var list = await _db.Set<AdoptionReference>().AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ReferenceDto> AddReferenceAsync(Guid requestId, string name, string contact, string? relationship, CancellationToken ct = default)
    {
        var r = await RequestAsParticipantAsync(requestId, ct); // applicant supplies references, owner may add too
        name = (name ?? "").Trim(); contact = (contact ?? "").Trim();
        if (name.Length is < 1 or > 200 || contact.Length is < 1 or > 300) throw new ConflictException("Name and contact are required.");
        if (await _db.Set<AdoptionReference>().CountAsync(x => x.RequestId == requestId, ct) >= 5) throw new ConflictException("At most 5 references.");
        var x = new AdoptionReference { RequestId = r.Id, Name = name, Contact = contact, Relationship = relationship?.Trim() };
        _db.Set<AdoptionReference>().Add(x);
        await _db.SaveChangesAsync(ct);
        return ToDto(x);
    }

    public async Task<ReferenceDto> UpdateReferenceAsync(Guid referenceId, AdoptionReferenceStatus status, string? notes, CancellationToken ct = default)
    {
        var x = await _db.Set<AdoptionReference>().SingleOrDefaultAsync(a => a.Id == referenceId, ct) ?? throw new NotFoundException("AdoptionReference", referenceId);
        await RequestAsOwnerAsync(x.RequestId, ct);
        x.Status = status; x.Notes = notes?.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(x);
    }

    public async Task<IReadOnlyList<HomeVisitDto>> ListHomeVisitsAsync(Guid requestId, CancellationToken ct = default)
    {
        await RequestAsParticipantAsync(requestId, ct);
        var list = await _db.Set<AdoptionHomeVisit>().AsNoTracking().Where(x => x.RequestId == requestId).OrderBy(x => x.ScheduledAt).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<HomeVisitDto> ScheduleHomeVisitAsync(Guid requestId, DateTime scheduledAt, string? notes, CancellationToken ct = default)
    {
        var r = await RequestAsOwnerAsync(requestId, ct);
        if (scheduledAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-5)) throw new ConflictException("Visit must be in the future.");
        var x = new AdoptionHomeVisit { RequestId = r.Id, ScheduledAt = scheduledAt.ToUniversalTime(), Notes = notes?.Trim() };
        _db.Set<AdoptionHomeVisit>().Add(x);
        _db.Notifications.Add(new InAppNotification
        {
            UserId = r.RequesterId, Title = "Home visit scheduled",
            Body = $"A home visit for {r.AdoptionListing.PetName ?? r.AdoptionListing.Title} is set for {x.ScheduledAt:u}.",
            Url = "/adoption/my-applications"
        });
        await _db.SaveChangesAsync(ct);
        return ToDto(x);
    }

    public async Task<HomeVisitDto> UpdateHomeVisitAsync(Guid visitId, AdoptionHomeVisitStatus status, string? notes, string? outcome, CancellationToken ct = default)
    {
        var x = await _db.Set<AdoptionHomeVisit>().SingleOrDefaultAsync(a => a.Id == visitId, ct) ?? throw new NotFoundException("AdoptionHomeVisit", visitId);
        await RequestAsOwnerAsync(x.RequestId, ct);
        x.Status = status; x.Notes = notes?.Trim(); x.Outcome = outcome?.Trim();
        await _db.SaveChangesAsync(ct);
        return ToDto(x);
    }

    // ---------- Follow-ups ----------

    public async Task<IReadOnlyList<FollowUpDto>> ListMyFollowUpsAsync(CancellationToken ct = default)
    {
        var uid = Uid;
        return await _db.Set<AdoptionFollowUp>().AsNoTracking().Where(f => f.AdopterId == uid).OrderBy(f => f.DueAt)
            .Select(f => new FollowUpDto(f.Id, f.ListingId, f.Listing.Title, f.DayOffset, f.DueAt, f.Status, f.Notes, f.WelfareScore, f.CompletedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FollowUpDto>> ListFollowUpsForListingAsync(Guid listingId, CancellationToken ct = default)
    {
        var uid = Uid;
        if (!await _db.AdoptionListings.AnyAsync(l => l.Id == listingId && l.OwnerId == uid, ct)) throw new ForbiddenException();
        return await _db.Set<AdoptionFollowUp>().AsNoTracking().Where(f => f.ListingId == listingId).OrderBy(f => f.DueAt)
            .Select(f => new FollowUpDto(f.Id, f.ListingId, f.Listing.Title, f.DayOffset, f.DueAt, f.Status, f.Notes, f.WelfareScore, f.CompletedAt)).ToListAsync(ct);
    }

    public async Task CompleteFollowUpAsync(Guid followUpId, int welfareScore, string? notes, CancellationToken ct = default)
    {
        var uid = Uid;
        if (welfareScore is < 1 or > 5) throw new ConflictException("Welfare score must be between 1 and 5.");
        var f = await _db.Set<AdoptionFollowUp>().Include(x => x.Listing).SingleOrDefaultAsync(x => x.Id == followUpId, ct)
            ?? throw new NotFoundException("AdoptionFollowUp", followUpId);
        if (f.AdopterId != uid) throw new ForbiddenException();
        f.Status = AdoptionFollowUpStatus.Completed; f.WelfareScore = welfareScore; f.Notes = notes?.Trim(); f.CompletedAt = DateTime.UtcNow;
        if (welfareScore <= 2)
            _db.Notifications.Add(new InAppNotification
            {
                UserId = f.Listing.OwnerId, Title = "Follow-up needs attention",
                Body = $"The adopter of {f.Listing.PetName ?? f.Listing.Title} reported a low welfare score at day {f.DayOffset}.",
                Url = $"/adoption/{f.ListingId}"
            });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<FollowUpSummaryDto> GetOutcomeSummaryAsync(Guid listingId, CancellationToken ct = default)
    {
        var uid = Uid;
        if (!await _db.AdoptionListings.AnyAsync(l => l.Id == listingId && l.OwnerId == uid, ct)) throw new ForbiddenException();
        var rows = await _db.Set<AdoptionFollowUp>().AsNoTracking().Where(f => f.ListingId == listingId).Select(f => new { f.Status, f.WelfareScore }).ToListAsync(ct);
        var scored = rows.Where(r => r.WelfareScore != null).Select(r => r.WelfareScore!.Value).ToList();
        return new FollowUpSummaryDto(rows.Count, rows.Count(r => r.Status == AdoptionFollowUpStatus.Completed),
            scored.Count == 0 ? null : Math.Round(scored.Average(), 2), scored.Count(s => s <= 2));
    }

    // ---------- Foster ----------

    public async Task<IReadOnlyList<FosterDto>> ListFostersAsync(Guid listingId, CancellationToken ct = default)
    {
        var uid = Uid;
        if (!await _db.AdoptionListings.AnyAsync(l => l.Id == listingId && l.OwnerId == uid, ct)) throw new ForbiddenException();
        var rows = await _db.Set<FosterPlacement>().AsNoTracking().Where(f => f.ListingId == listingId).OrderByDescending(f => f.StartedAt).ToListAsync(ct);
        var fosterIds = rows.Select(r => r.FosterUserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => fosterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return rows.Select(f => new FosterDto(f.Id, f.ListingId, f.PetId, f.FosterUserId, names.GetValueOrDefault(f.FosterUserId, "?"), f.StartedAt, f.EndedAt, f.Outcome, f.Notes)).ToList();
    }

    public async Task<FosterDto> StartFosterAsync(Guid listingId, Guid fosterUserId, string? notes, CancellationToken ct = default)
    {
        var uid = Uid;
        var l = await _db.AdoptionListings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == listingId, ct) ?? throw new NotFoundException("AdoptionListing", listingId);
        if (l.OwnerId != uid) throw new ForbiddenException();
        if (fosterUserId == uid) throw new ConflictException("Choose a different user as foster.");
        var name = await _db.Users.Where(u => u.Id == fosterUserId && u.IsActive && !u.IsSuspended).Select(u => u.DisplayName).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", fosterUserId);
        if (await _db.Set<FosterPlacement>().AnyAsync(f => f.ListingId == listingId && f.EndedAt == null, ct))
            throw new ConflictException("This listing already has an active foster placement.");
        var f = new FosterPlacement { ListingId = listingId, PetId = l.PetId, FosterUserId = fosterUserId, Notes = notes?.Trim() };
        _db.Set<FosterPlacement>().Add(f);
        _db.Notifications.Add(new InAppNotification { UserId = fosterUserId, Title = "Foster placement started", Body = $"You are fostering {l.PetName ?? l.Title}.", Url = $"/adoption/{listingId}" });
        await _db.SaveChangesAsync(ct);
        return new FosterDto(f.Id, listingId, f.PetId, fosterUserId, name, f.StartedAt, null, f.Outcome, f.Notes);
    }

    public async Task EndFosterAsync(Guid placementId, FosterOutcome outcome, string? notes, CancellationToken ct = default)
    {
        var uid = Uid;
        if (outcome == FosterOutcome.Ongoing) throw new ConflictException("Choose an outcome.");
        var f = await _db.Set<FosterPlacement>().Include(x => x.Listing).SingleOrDefaultAsync(x => x.Id == placementId, ct)
            ?? throw new NotFoundException("FosterPlacement", placementId);
        if (f.Listing.OwnerId != uid) throw new ForbiddenException();
        if (f.EndedAt != null) throw new ConflictException("Placement already ended.");
        f.Outcome = outcome; f.EndedAt = DateTime.UtcNow; f.Notes = notes?.Trim() ?? f.Notes;
        await _db.SaveChangesAsync(ct);
        if (outcome is FosterOutcome.ConvertedToAdoption or FosterOutcome.FosterFailed)
        {
            // "Foster fail" and conversion both end in the foster adopting: feeds the existing mark-adopted flow.
            await _listings.MarkAdoptedAsync(f.ListingId, f.FosterUserId, false, false, ct);
        }
    }

    // ---------- Adopter profile + match ----------

    private static AdopterProfileDto ToDto(AdopterProfile p) => new(p.HouseholdSize, p.ChildrenAges, p.ExistingPetsJson, p.Experience,
        p.HomeType, p.HasYard, p.TravelDistanceKm, p.Activity, p.Latitude, p.Longitude);

    public async Task<AdopterProfileDto?> GetProfileAsync(CancellationToken ct = default)
    {
        var uid = Uid;
        var p = await _db.Set<AdopterProfile>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid, ct);
        return p is null ? null : ToDto(p);
    }

    public async Task<AdopterProfileDto> UpsertProfileAsync(AdopterProfileDto d, CancellationToken ct = default)
    {
        var uid = Uid;
        if (d.HouseholdSize is < 1 or > 30) throw new ConflictException("Household size must be 1-30.");
        if (d.TravelDistanceKm is < 1 or > 2000) throw new ConflictException("Travel distance must be 1-2000 km.");
        if (d.ExistingPetsJson is { Length: > 0 })
        {
            try { JsonDocument.Parse(d.ExistingPetsJson).Dispose(); } catch (JsonException) { throw new ConflictException("Existing pets must be valid JSON."); }
        }
        var (lat, lng) = GeoSearch.Approximate(d.Latitude, d.Longitude);
        var p = await _db.Set<AdopterProfile>().SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (p is null) { p = new AdopterProfile { UserId = uid }; _db.Set<AdopterProfile>().Add(p); }
        p.HouseholdSize = d.HouseholdSize; p.ChildrenAges = d.ChildrenAges?.Trim(); p.ExistingPetsJson = d.ExistingPetsJson;
        p.Experience = d.Experience; p.HomeType = d.HomeType; p.HasYard = d.HasYard; p.TravelDistanceKm = d.TravelDistanceKm;
        p.Activity = d.Activity; p.Latitude = lat; p.Longitude = lng; p.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    private static AdopterFacts ToFacts(AdopterProfile p)
    {
        var kids = (p.ChildrenAges ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : -1).Where(n => n >= 0).ToList();
        var pets = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.ExistingPetsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(p.ExistingPetsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var el in doc.RootElement.EnumerateArray())
                        pets.Add(el.ValueKind == JsonValueKind.Object && el.TryGetProperty("type", out var t) ? t.ToString() : el.ToString());
            }
            catch (JsonException) { }
        }
        return new AdopterFacts(p.HouseholdSize, kids, pets, p.Experience, p.HomeType, p.HasYard, p.TravelDistanceKm, p.Activity, p.Latitude, p.Longitude);
    }

    private async Task<Dictionary<(Guid User, Guid Listing), MatchResult>> ComputeMatchesAsync(IReadOnlyList<(Guid User, Guid Listing)> pairs, CancellationToken ct)
    {
        var result = new Dictionary<(Guid, Guid), MatchResult>();
        if (pairs.Count == 0) return result;
        var userIds = pairs.Select(p => p.User).Distinct().ToList();
        var listingIds = pairs.Select(p => p.Listing).Distinct().ToList();
        var profiles = await _db.Set<AdopterProfile>().AsNoTracking().Where(p => userIds.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, ct);
        // Preferred species comes from the adopter's most recent wanted post (the profile has no species field).
        var wanted = await _db.Set<AdoptionWantedPost>().AsNoTracking().Where(w => userIds.Contains(w.RequesterId))
            .OrderByDescending(w => w.CreatedAt).Select(w => new { w.RequesterId, w.AnimalType }).ToListAsync(ct);
        var preferred = new Dictionary<Guid, AnimalType>();
        foreach (var w in wanted) preferred.TryAdd(w.RequesterId, w.AnimalType);
        var listingRows = await _db.AdoptionListings.AsNoTracking().Where(l => listingIds.Contains(l.Id))
            .Select(l => new { l.Id, l.AnimalType, l.Size, l.GoodWithChildren, l.GoodWithOtherPets, l.AgeMonths, l.BehaviorNotes, l.Latitude, l.Longitude })
            .ToListAsync(ct);
        var byId = listingRows.ToDictionary(l => l.Id, l => new ListingFacts(l.AnimalType, l.Size, l.GoodWithChildren,
            l.GoodWithOtherPets, l.AgeMonths, l.BehaviorNotes, l.Latitude, l.Longitude));
        foreach (var (u, l) in pairs)
            if (profiles.TryGetValue(u, out var prof) && byId.TryGetValue(l, out var lf))
                result[(u, l)] = MatchScoring.Score(ToFacts(prof) with { PreferredType = preferred.TryGetValue(u, out var pt) ? pt : null }, lf);
        return result;
    }

    public async Task<IReadOnlyList<MatchDto>> MatchListingsAsync(IReadOnlyList<Guid> listingIds, CancellationToken ct = default)
    {
        var uid = Uid;
        var ids = listingIds.Distinct().Take(60).ToList();
        var m = await ComputeMatchesAsync(ids.Select(i => (uid, i)).ToList(), ct);
        return m.Select(kv => new MatchDto(kv.Key.Listing, kv.Value.Percent, kv.Value.Explanations)).ToList();
    }

    // ---------- Risk ----------

    public async Task<IReadOnlyList<RiskFlagDto>> GetRiskFlagsAsync(IReadOnlyList<Guid> listingIds, CancellationToken ct = default)
    {
        _ = Uid;
        if (!_current.Permissions.Contains(Application.Common.Permissions.Permissions.Adoption.Approve)
            && !_current.Permissions.Contains(Application.Common.Permissions.Permissions.Adoption.Reject))
            throw new ForbiddenException();
        var ids = listingIds.Distinct().Take(200).ToList();
        return await _db.Set<AdoptionListingRiskFlag>().AsNoTracking().Where(f => ids.Contains(f.ListingId))
            .OrderByDescending(f => f.Severity).Select(f => new RiskFlagDto(f.ListingId, f.Kind, f.Detail, f.Severity, f.CreatedAt)).ToListAsync(ct);
    }
}
