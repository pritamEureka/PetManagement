using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Events;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class PetSharingService : IPetSharingService
{
    // No 0/O/1/I/L — codes get read aloud and typed from collar tags.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public PetSharingService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access,
        IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _access = access;
        _kafka = kafka;
        _audit = audit;
    }

    // ---------- Share links ----------

    public async Task<IReadOnlyList<ShareLinkDto>> ListShareLinksAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        return await _db.PetHealthShareLinks.AsNoTracking().Where(l => l.PetId == petId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new ShareLinkDto(l.Id, l.Token, l.Label, l.ExpiresAt, l.RevokedAt, l.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ShareLinkDto> CreateShareLinkAsync(Guid petId, CreateShareLinkInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var link = new PetHealthShareLink
        {
            PetId = petId,
            Token = Base64Url(RandomNumberGenerator.GetBytes(32)),
            ExpiresAt = DateTime.UtcNow.AddDays(input.ValidDays),
            Label = Clean(input.Label)
        };
        _db.PetHealthShareLinks.Add(link);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("share_link_created", "Pet", petId.ToString(), "Pets",
            newValues: new { link.Id, link.ExpiresAt, link.Label }, ct: ct);
        return new ShareLinkDto(link.Id, link.Token, link.Label, link.ExpiresAt, null, link.CreatedAt);
    }

    public async Task RevokeShareLinkAsync(Guid petId, Guid linkId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var link = await _db.PetHealthShareLinks.SingleOrDefaultAsync(l => l.Id == linkId && l.PetId == petId, ct)
            ?? throw new NotFoundException("ShareLink", linkId);
        link.RevokedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Health report ----------

    public async Task<HealthReportDto> GetHealthReportAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var level = await _access.GetLevelAsync(petId, ct);
        return await BuildReportAsync(petId, level, null, ct);
    }

    public async Task<HealthReportDto?> GetSharedHealthReportAsync(string token, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var link = await _db.PetHealthShareLinks.AsNoTracking()
            .SingleOrDefaultAsync(l => l.Token == token && l.RevokedAt == null && l.ExpiresAt > now, ct);
        if (link is null) return null;
        return await BuildReportAsync(link.PetId, PetAccessLevel.Viewer, link.ExpiresAt, ct);
    }

    private async Task<HealthReportDto> BuildReportAsync(Guid petId, PetAccessLevel level, DateTime? expiresAt, CancellationToken ct)
    {
        var p = await _db.Pets.AsNoTracking()
            .Include(x => x.Owner).Include(x => x.Group).Include(x => x.Sire).Include(x => x.Dam)
            .Include(x => x.Vaccinations).Include(x => x.MedicalRecords).Include(x => x.GroomingRecords)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == petId, ct) ?? throw new NotFoundException("Pet", petId);

        // Photos aren't included — they aren't part of a health export.
        var detail = ToDetail(p, level);

        var weights = await _db.PetWeightLogs.AsNoTracking().Where(w => w.PetId == petId).OrderBy(w => w.RecordedOn)
            .Select(w => new WeightLogDto(w.Id, w.RecordedOn, w.WeightKg, w.Notes)).ToListAsync(ct);
        var meds = await _db.PetMedications.AsNoTracking().Where(m => m.PetId == petId).OrderByDescending(m => m.StartDate)
            .Select(m => new MedicationDto(m.Id, m.PetId, m.PrescriptionId, m.DrugName, m.Dose, m.DoseTimes, m.StartDate,
                m.EndDate, m.Instructions, m.IsActive, 0, 0)).ToListAsync(ct);

        return new HealthReportDto(detail, weights, meds, DateTime.UtcNow, expiresAt);
    }

    // ---------- Public QR profile ----------

    public async Task<PublicProfileStateDto> SetPublicProfileAsync(Guid petId, PublicProfileInput input, CancellationToken ct = default)
    {
        await _access.EnsureOwnerAsync(petId, ct);
        var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
        pet.PublicProfileEnabled = input.Enabled;
        if (input.Enabled && pet.PublicCode is null)
        {
            // Code stays stable once issued so printed tags keep working after a disable/enable.
            for (var attempt = 0; attempt < 10 && pet.PublicCode is null; attempt++)
            {
                var code = RandomCode(8);
                if (!await _db.Pets.IgnoreQueryFilters().AnyAsync(p => p.PublicCode == code, ct)) pet.PublicCode = code;
            }
            if (pet.PublicCode is null) throw new ConflictException("Couldn't allocate a public code, please retry.");
        }
        await _db.SaveChangesAsync(ct);
        return new PublicProfileStateDto(pet.PublicProfileEnabled, pet.PublicCode);
    }

    public async Task<PublicPetDto?> GetPublicPetAsync(string code, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        var p = await _db.Pets.AsNoTracking()
            .Where(x => x.PublicCode == code && x.PublicProfileEnabled)
            .Select(x => new
            {
                x.Id, x.Name, x.AnimalType, x.Breed, x.Gender, x.Color, x.PrimaryPhotoUrl, x.Allergies, x.IsLost,
                OwnerName = x.Owner.DisplayName
            }).SingleOrDefaultAsync(ct);
        if (p is null) return null;

        var openReport = await _db.LostFoundReports.AsNoTracking()
            .Where(r => r.PetId == p.Id && r.Kind == LostFoundKind.Lost && r.Status == LostFoundStatus.Open)
            .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

        var firstName = p.OwnerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "the owner";
        return new PublicPetDto(code, p.Name, p.AnimalType, p.Breed, p.Gender, p.Color, p.PrimaryPhotoUrl,
            p.Allergies, p.IsLost, firstName, openReport);
    }

    public async Task ReportSightingAsync(string code, SightingInput input, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        var pet = await _db.Pets.AsNoTracking()
            .Where(x => x.PublicCode == code && x.PublicProfileEnabled)
            .Select(x => new { x.Id, x.Name, x.OwnerId }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Pet", code);

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(input.Message)) parts.Add(input.Message.Trim());
        if (!string.IsNullOrWhiteSpace(input.ContactInfo)) parts.Add($"Contact: {input.ContactInfo.Trim()}");
        if (input.Latitude is { } lat && input.Longitude is { } lng)
            parts.Add($"Location: https://maps.google.com/?q={lat:F5},{lng:F5}");
        if (_current.UserId is { } finder)
        {
            var name = await _db.Users.Where(u => u.Id == finder).Select(u => u.DisplayName).SingleOrDefaultAsync(ct);
            parts.Add($"Reported by {name} (signed in)");
        }

        _db.Notifications.Add(new InAppNotification
        {
            UserId = pet.OwnerId,
            Title = $"Someone scanned {pet.Name}'s tag",
            Body = parts.Count > 0 ? string.Join(" · ", parts) : "The QR tag was scanned — no message was left.",
            Url = $"/pets/{pet.Id}"
        });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events,
            new PetSightingReported(pet.Id, pet.OwnerId, input.Latitude, input.Longitude, DateTime.UtcNow), pet.Id.ToString(), ct);
    }

    // ---------- Ownership transfer ----------

    public async Task TransferOwnershipAsync(Guid petId, Guid toUserId, Guid? adoptionListingId, bool includeHealthHistory,
        CancellationToken ct = default)
    {
        var pet = await _db.Pets.SingleOrDefaultAsync(p => p.Id == petId, ct) ?? throw new NotFoundException("Pet", petId);
        if (pet.OwnerId == toUserId) return;
        if (!await _db.Users.AnyAsync(u => u.Id == toUserId, ct)) throw new NotFoundException("User", toUserId);

        var fromUserId = pet.OwnerId;
        var now = DateTime.UtcNow;

        pet.OwnerId = toUserId;
        pet.IsAvailableForAdoption = false;
        pet.PublicProfileEnabled = false;
        pet.GroupId = null;

        // Access that belonged to the previous household ends with the hand-over.
        await _db.PetCaretakers.Where(c => c.PetId == petId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsDeleted, true).SetProperty(c => c.DeletedAt, now), ct);
        await _db.PetHealthShareLinks.Where(l => l.PetId == petId && l.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RevokedAt, now), ct);
        // Money is private to the previous owner, always.
        await _db.PetExpenses.Where(e => e.PetId == petId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);
        await _db.PetInsurancePolicies.Where(e => e.PetId == petId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);

        if (!includeHealthHistory)
        {
            // Vaccinations always travel with the animal (welfare + legal); the
            // rest is withheld when the previous owner didn't consent. Rows are
            // soft-deleted so the audit trail survives.
            await _db.MedicalRecords.Where(e => e.PetId == petId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);
            await _db.GroomingRecords.Where(e => e.PetId == petId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);
            await _db.PetMedications.Where(e => e.PetId == petId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);
            await _db.PetWeightLogs.Where(e => e.PetId == petId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsDeleted, true).SetProperty(e => e.DeletedAt, now), ct);
        }

        _db.PetOwnershipTransfers.Add(new PetOwnershipTransfer
        {
            PetId = petId, FromUserId = fromUserId, ToUserId = toUserId,
            AdoptionListingId = adoptionListingId, IncludesHealthHistory = includeHealthHistory
        });
        _db.Notifications.Add(new InAppNotification
        {
            UserId = toUserId,
            Title = $"{pet.Name} is now yours 🐾",
            Body = includeHealthHistory
                ? "The pet profile and its health history were transferred to your account."
                : "The pet profile and vaccination history were transferred to your account.",
            Url = $"/pets/{petId}"
        });
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("ownership_transferred", "Pet", petId.ToString(), "Pets",
            oldValues: new { ownerId = fromUserId }, newValues: new { ownerId = toUserId, adoptionListingId, includeHealthHistory }, ct: ct);
        await _kafka.PublishAsync(PetTopics.Events,
            new PetOwnershipTransferred(petId, fromUserId, toUserId, adoptionListingId, now), petId.ToString(), ct);
    }

    private static string RandomCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++) chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
