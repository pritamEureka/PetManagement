using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Events;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class PetService : IPetService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public PetService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access,
        IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _access = access;
        _kafka = kafka;
        _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // ---------- Profile ----------

    public async Task<IReadOnlyList<PetSummaryDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var pets = await PetAccessService.AccessiblePets(_db, uid).AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                Pet = p,
                GroupName = p.Group != null ? p.Group.Name : null,
                Role = p.OwnerId == uid
                    ? (CaretakerRole?)null
                    : p.Caretakers.Where(c => c.UserId == uid).Select(c => (CaretakerRole?)c.Role).FirstOrDefault(),
                Vaccines = p.Vaccinations.Select(v => new { v.VaccineName, v.AdministeredOn, v.NextDueOn }).ToList()
            })
            .ToListAsync(ct);

        return pets.Select(x =>
        {
            var due = LatestPerVaccine(x.Vaccines.Select(v => (v.VaccineName, v.AdministeredOn, v.NextDueOn)))
                .Where(v => v.NextDueOn.HasValue).Select(v => v.NextDueOn!.Value).ToList();
            var p = x.Pet;
            return new PetSummaryDto(p.Id, p.OwnerId, p.Name, p.AnimalType, p.Breed, p.Gender, p.BirthDate, p.WeightKg,
                p.Color, p.TagNumber, p.PrimaryPhotoUrl, p.Allergies, p.DietNotes, p.IsAvailableForAdoption, p.Size,
                p.MicrochipNumber, p.IsLost, p.GroupId, x.GroupName, ToLevel(x.Role),
                due.Count(d => d < now), due.Where(d => d >= now).OrderBy(d => d).Cast<DateTime?>().FirstOrDefault(),
                p.CreatedAt);
        }).ToList();
    }

    public async Task<PetDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(id, ct);
        var level = await _access.GetLevelAsync(id, ct);

        var p = await _db.Pets.AsNoTracking()
            .Include(x => x.Owner)
            .Include(x => x.Group)
            .Include(x => x.Sire)
            .Include(x => x.Dam)
            .Include(x => x.Photos)
            .Include(x => x.Vaccinations)
            .Include(x => x.MedicalRecords)
            .Include(x => x.GroomingRecords)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Pet", id);

        return ToDetail(p, level);
    }

    internal static PetDetailDto ToDetail(Pet p, PetAccessLevel level) => new(
        p.Id, p.OwnerId, p.Owner?.DisplayName ?? "", p.Name, p.AnimalType, p.Breed, p.Gender, p.BirthDate, p.WeightKg,
        p.Color, p.TagNumber, p.PrimaryPhotoUrl, p.Allergies, p.DietNotes, p.IsAvailableForAdoption, p.Size,
        p.MicrochipNumber,
        level >= PetAccessLevel.CoOwner ? p.PublicCode : null,
        p.PublicProfileEnabled, p.IsLost,
        p.Sire is null ? null : new PetRefDto(p.Sire.Id, p.Sire.Name, p.Sire.Breed, p.Sire.PrimaryPhotoUrl),
        p.Dam is null ? null : new PetRefDto(p.Dam.Id, p.Dam.Name, p.Dam.Breed, p.Dam.PrimaryPhotoUrl),
        p.GroupId, p.Group?.Name, level, p.CreatedAt,
        p.Photos.OrderByDescending(x => x.UploadedAt).Select(x => new PetPhotoDto(x.Id, x.Url, x.Caption, x.UploadedAt)).ToList(),
        p.Vaccinations.OrderByDescending(x => x.AdministeredOn).Select(ToDto).ToList(),
        p.MedicalRecords.OrderByDescending(x => x.VisitDate).Select(ToDto).ToList(),
        p.GroomingRecords.OrderByDescending(x => x.PerformedOn).Select(ToDto).ToList());

    public async Task<PetDetailDto> CreateAsync(UpsertPetInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await ValidateRelationsAsync(null, input, uid, ct);

        var pet = new Pet { OwnerId = uid };
        Apply(pet, input);
        _db.Pets.Add(pet);
        if (input.WeightKg is > 0)
            _db.PetWeightLogs.Add(new PetWeightLog { PetId = pet.Id, RecordedOn = DateTime.UtcNow, WeightKg = input.WeightKg.Value });

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events, new PetCreated(pet.Id, uid, pet.AnimalType.ToString(), DateTime.UtcNow), pet.Id.ToString(), ct);
        return await GetAsync(pet.Id, ct);
    }

    public async Task UpdateAsync(Guid id, UpsertPetInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(id, ct);
        var pet = await _db.Pets.SingleAsync(p => p.Id == id, ct);
        await ValidateRelationsAsync(id, input, pet.OwnerId, ct);

        var weightChanged = input.WeightKg is > 0 && input.WeightKg != pet.WeightKg;
        Apply(pet, input);
        if (weightChanged)
            _db.PetWeightLogs.Add(new PetWeightLog { PetId = id, RecordedOn = DateTime.UtcNow, WeightKg = input.WeightKg!.Value });
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _access.EnsureOwnerAsync(id, ct);
        var pet = await _db.Pets.SingleAsync(p => p.Id == id, ct);
        _db.Pets.Remove(pet);
        await _db.PetCaretakers.Where(c => c.PetId == id).ExecuteUpdateAsync(s => s
            .SetProperty(c => c.IsDeleted, true).SetProperty(c => c.DeletedAt, DateTime.UtcNow), ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("delete", "Pet", id.ToString(), "Pets", ct: ct);
        await _kafka.PublishAsync(PetTopics.Events, new PetDeleted(id, pet.OwnerId, DateTime.UtcNow), id.ToString(), ct);
    }

    private static void Apply(Pet pet, UpsertPetInput i)
    {
        pet.Name = i.Name.Trim();
        pet.AnimalType = i.AnimalType;
        pet.Breed = Clean(i.Breed);
        pet.Gender = i.Gender;
        pet.BirthDate = i.BirthDate.HasValue ? DateTime.SpecifyKind(i.BirthDate.Value.Date, DateTimeKind.Utc) : null;
        pet.WeightKg = i.WeightKg is > 0 ? i.WeightKg : null;
        pet.Color = Clean(i.Color);
        pet.TagNumber = Clean(i.TagNumber);
        pet.PrimaryPhotoUrl = Clean(i.PrimaryPhotoUrl);
        pet.Allergies = Clean(i.Allergies);
        pet.DietNotes = Clean(i.DietNotes);
        pet.IsAvailableForAdoption = i.IsAvailableForAdoption;
        pet.Size = i.Size;
        pet.MicrochipNumber = Clean(i.MicrochipNumber);
        pet.SireId = i.SireId;
        pet.DamId = i.DamId;
        pet.GroupId = i.GroupId;
    }

    private async Task ValidateRelationsAsync(Guid? selfId, UpsertPetInput i, Guid ownerId, CancellationToken ct)
    {
        if (selfId.HasValue && (i.SireId == selfId || i.DamId == selfId))
            throw new ConflictException("A pet can't be its own parent.");

        foreach (var (parentId, label, gender) in new[] { (i.SireId, "Sire", Gender.Male), (i.DamId, "Dam", Gender.Female) })
        {
            if (parentId is null) continue;
            var parent = await _db.Pets.AsNoTracking().Where(p => p.Id == parentId)
                .Select(p => new { p.OwnerId, p.PublicProfileEnabled, p.Gender, p.AnimalType }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(label, parentId);
            // Only link to your own animals or ones whose owners made them public.
            if (parent.OwnerId != ownerId && !parent.PublicProfileEnabled)
                throw new ForbiddenException($"{label} must be one of your animals or have a public profile.");
            if (parent.Gender != Gender.Unknown && parent.Gender != gender)
                throw new ConflictException($"{label} must be {gender.ToString().ToLowerInvariant()}.");
            if (parent.AnimalType != i.AnimalType)
                throw new ConflictException($"{label} must be the same species.");
        }

        if (i.GroupId is not null
            && !await _db.PetGroups.AnyAsync(g => g.Id == i.GroupId && g.OwnerId == ownerId, ct))
            throw new NotFoundException("Group", i.GroupId);

        if (!string.IsNullOrWhiteSpace(i.MicrochipNumber)
            && await _db.Pets.AnyAsync(p => p.MicrochipNumber == i.MicrochipNumber.Trim() && p.Id != selfId, ct))
            throw new ConflictException("That microchip number is already registered to another pet.");
    }

    // ---------- Photos ----------

    public async Task<PetPhotoDto> AddPhotoAsync(Guid petId, PetPhotoInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
        if (await _db.PetPhotos.CountAsync(x => x.PetId == petId, ct) >= 50)
            throw new ConflictException("A pet can have at most 50 photos.");

        var photo = new PetPhoto { PetId = petId, Url = input.Url.Trim(), Caption = Clean(input.Caption) };
        _db.PetPhotos.Add(photo);
        if (input.MakePrimary || string.IsNullOrEmpty(pet.PrimaryPhotoUrl)) pet.PrimaryPhotoUrl = photo.Url;
        await _db.SaveChangesAsync(ct);
        return new PetPhotoDto(photo.Id, photo.Url, photo.Caption, photo.UploadedAt);
    }

    public async Task SetPrimaryPhotoAsync(Guid petId, Guid photoId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var photo = await _db.PetPhotos.SingleOrDefaultAsync(x => x.Id == photoId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Photo", photoId);
        var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
        pet.PrimaryPhotoUrl = photo.Url;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePhotoAsync(Guid petId, Guid photoId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var photo = await _db.PetPhotos.SingleOrDefaultAsync(x => x.Id == photoId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Photo", photoId);
        var pet = await _db.Pets.SingleAsync(p => p.Id == petId, ct);
        _db.PetPhotos.Remove(photo);
        if (pet.PrimaryPhotoUrl == photo.Url)
        {
            pet.PrimaryPhotoUrl = await _db.PetPhotos.Where(x => x.PetId == petId && x.Id != photoId)
                .OrderByDescending(x => x.UploadedAt).Select(x => x.Url).FirstOrDefaultAsync(ct);
        }
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Vaccinations ----------

    public async Task<VaccinationDto> AddVaccinationAsync(Guid petId, VaccinationInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = new VaccinationRecord { PetId = petId };
        Apply(rec, input);
        _db.VaccinationRecords.Add(rec);
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events,
            new PetVaccinationRecorded(petId, rec.Id, rec.VaccineName, rec.NextDueOn, DateTime.UtcNow), petId.ToString(), ct);
        return ToDto(rec);
    }

    public async Task UpdateVaccinationAsync(Guid petId, Guid recordId, VaccinationInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = await _db.VaccinationRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Vaccination", recordId);
        var dueChanged = rec.NextDueOn != input.NextDueOn;
        Apply(rec, input);
        if (dueChanged) rec.LastReminderStage = null;   // re-arm reminders for the new date
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteVaccinationAsync(Guid petId, Guid recordId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = await _db.VaccinationRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Vaccination", recordId);
        _db.VaccinationRecords.Remove(rec);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(VaccinationRecord r, VaccinationInput i)
    {
        r.VaccineName = i.VaccineName.Trim();
        r.AdministeredOn = Utc(i.AdministeredOn);
        r.NextDueOn = i.NextDueOn.HasValue ? Utc(i.NextDueOn.Value) : null;
        r.AdministeredByVet = Clean(i.AdministeredByVet);
        r.Notes = Clean(i.Notes);
        r.CertificateUrl = Clean(i.CertificateUrl);
    }

    // ---------- Medical records ----------

    public async Task<MedicalRecordDto> AddMedicalRecordAsync(Guid petId, MedicalRecordInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = new MedicalRecord { PetId = petId };
        Apply(rec, input);
        _db.MedicalRecords.Add(rec);
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events, new PetMedicalRecordAdded(petId, rec.Id, null, DateTime.UtcNow), petId.ToString(), ct);
        return ToDto(rec);
    }

    public async Task UpdateMedicalRecordAsync(Guid petId, Guid recordId, MedicalRecordInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = await _db.MedicalRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("MedicalRecord", recordId);
        Apply(rec, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteMedicalRecordAsync(Guid petId, Guid recordId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var rec = await _db.MedicalRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("MedicalRecord", recordId);
        if (rec.AppointmentId.HasValue)
            throw new ConflictException("Records created from a vet appointment can't be deleted.");
        _db.MedicalRecords.Remove(rec);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(MedicalRecord r, MedicalRecordInput i)
    {
        r.VisitDate = Utc(i.VisitDate);
        r.Diagnosis = Clean(i.Diagnosis);
        r.Treatment = Clean(i.Treatment);
        r.Notes = Clean(i.Notes);
        r.VetName = Clean(i.VetName);
        r.AttachmentUrl = Clean(i.AttachmentUrl);
        r.PrescriptionFileUrl = Clean(i.PrescriptionFileUrl) ?? r.PrescriptionFileUrl;
    }

    // ---------- Grooming ----------

    public async Task<GroomingRecordDto> AddGroomingRecordAsync(Guid petId, GroomingRecordInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var rec = new GroomingRecord { PetId = petId };
        Apply(rec, input);
        _db.GroomingRecords.Add(rec);
        await _db.SaveChangesAsync(ct);
        return ToDto(rec);
    }

    public async Task UpdateGroomingRecordAsync(Guid petId, Guid recordId, GroomingRecordInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var rec = await _db.GroomingRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("GroomingRecord", recordId);
        Apply(rec, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteGroomingRecordAsync(Guid petId, Guid recordId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var rec = await _db.GroomingRecords.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("GroomingRecord", recordId);
        _db.GroomingRecords.Remove(rec);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(GroomingRecord r, GroomingRecordInput i)
    {
        r.PerformedOn = Utc(i.PerformedOn);
        r.ServiceType = i.ServiceType.Trim();
        r.Provider = Clean(i.Provider);
        r.Notes = Clean(i.Notes);
        r.Cost = i.Cost;
    }

    // ---------- Vaccine schedule ----------

    public IReadOnlyList<VaccinePlanItem> GetVaccinePlan(AnimalType animalType) => PetKnowledge.VaccinePlan(animalType);

    public async Task<IReadOnlyList<VaccineScheduleItemDto>> GetVaccineScheduleAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId)
            .Select(p => new { p.AnimalType, p.BirthDate }).SingleAsync(ct);
        var given = await _db.VaccinationRecords.AsNoTracking().Where(v => v.PetId == petId)
            .Select(v => new { v.VaccineName, v.AdministeredOn }).ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        // Each plan item is matched to at most one given record, so "DHPP (1st)" and
        // "DHPP (2nd)" don't both claim the same shot.
        var used = new HashSet<int>();
        var result = new List<VaccineScheduleItemDto>();
        foreach (var item in PetKnowledge.VaccinePlan(pet.AnimalType).OrderBy(i => i.AgeWeeks))
        {
            var stem = Stem(item.VaccineName);
            var matchIdx = given.Select((g, idx) => (g, idx))
                .Where(x => !used.Contains(x.idx) && Stem(x.g.VaccineName).Contains(stem, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.g.AdministeredOn).Select(x => (int?)x.idx).FirstOrDefault();
            DateTime? dueOn = pet.BirthDate?.Date.AddDays(item.AgeWeeks * 7);

            if (matchIdx is int idx)
            {
                used.Add(idx);
                result.Add(new VaccineScheduleItemDto(item.VaccineName, item.AgeWeeks, dueOn, "given", given[idx].AdministeredOn, item.Notes));
                continue;
            }
            var status = dueOn is null ? "unknown"
                : dueOn < today ? "overdue"
                : dueOn <= today.AddDays(14) ? "due"
                : "upcoming";
            result.Add(new VaccineScheduleItemDto(item.VaccineName, item.AgeWeeks, dueOn, status, null, item.Notes));
        }
        return result;
    }

    private static string Stem(string vaccineName)
    {
        var i = vaccineName.IndexOf('(');
        var s = i > 0 ? vaccineName[..i] : vaccineName;
        return s.Replace("booster", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    // ---------- Recommendations ----------

    public async Task<RecommendationsDto> GetRecommendationsAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId)
            .Select(p => new { p.AnimalType, p.BirthDate, p.Size }).SingleAsync(ct);

        var stage = PetKnowledge.LifeStage(pet.AnimalType, pet.BirthDate);
        var species = PetKnowledge.SpeciesKeyword(pet.AnimalType);
        var stageKw = PetKnowledge.LifeStageKeyword(pet.AnimalType, stage);
        var sizeKw = pet.Size switch
        {
            AnimalSize.Tiny or AnimalSize.Small => "small",
            AnimalSize.Large or AnimalSize.ExtraLarge => "large",
            _ => null
        };

        var speciesLike = $"%{species}%";
        var candidates = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity > 0 && p.Store.ApprovalStatus == ApprovalStatus.Approved)
            .Where(p => EF.Functions.ILike(p.Name, speciesLike)
                     || (p.Description != null && EF.Functions.ILike(p.Description, speciesLike))
                     || (p.Category != null && EF.Functions.ILike(p.Category.Name, speciesLike)))
            .OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.RatingAverage)
            .Take(40)
            .Select(p => new
            {
                p.Id, p.Name, p.Description, p.Price, p.DiscountPrice,
                Image = p.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        bool Has(string text, string? kw) => kw is not null && text.Contains(kw, StringComparison.OrdinalIgnoreCase);
        var products = candidates
            .Select(p =>
            {
                var text = $"{p.Name} {p.Description}";
                var score = (Has(text, stageKw) ? 2 : 0) + (Has(text, sizeKw) ? 1 : 0);
                var reason = Has(text, stageKw) ? $"Made for {stage.ToLowerInvariant()}s"
                    : Has(text, sizeKw) ? $"Suited to {sizeKw} {species}s"
                    : $"Popular for {species}s";
                return (p, score, reason);
            })
            .OrderByDescending(x => x.score)
            .Take(8)
            .Select(x => new RecommendedProductDto(x.p.Id, x.p.Name, x.p.Price, x.p.DiscountPrice, x.p.Image, x.reason))
            .ToList();

        return new RecommendationsDto(stage, PetKnowledge.CareTips(pet.AnimalType, stage, pet.Size),
            PetKnowledge.VaccinePlan(pet.AnimalType), products);
    }

    // ---------- Upcoming care (dashboard) ----------

    public async Task<IReadOnlyList<UpcomingCareItemDto>> UpcomingCareAsync(int days, CancellationToken ct = default)
    {
        var uid = Uid();
        days = Math.Clamp(days, 1, 90);
        var now = DateTime.UtcNow;
        var horizon = now.AddDays(days);

        var pets = await PetAccessService.AccessiblePets(_db, uid).AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Name, p.BirthDate,
                Vaccines = p.Vaccinations.Select(v => new { v.VaccineName, v.AdministeredOn, v.NextDueOn }).ToList()
            }).ToListAsync(ct);

        var items = new List<UpcomingCareItemDto>();
        foreach (var p in pets)
        {
            foreach (var v in LatestPerVaccine(p.Vaccines.Select(v => (v.VaccineName, v.AdministeredOn, v.NextDueOn))))
            {
                if (v.NextDueOn is not { } due || due > horizon) continue;
                items.Add(new UpcomingCareItemDto("vaccination", p.Id, p.Name, $"{v.VaccineName} due", due, due < now,
                    $"/pets/{p.Id}?tab=health"));
            }

            if (p.BirthDate is { } bd)
            {
                var next = new DateTime(now.Year, bd.Month, Math.Min(bd.Day, DateTime.DaysInMonth(now.Year, bd.Month)), 0, 0, 0, DateTimeKind.Utc);
                if (next < now.Date) next = next.AddYears(1);
                if (next <= horizon)
                {
                    var age = next.Year - bd.Year;
                    items.Add(new UpcomingCareItemDto("birthday", p.Id, p.Name, $"{p.Name} turns {age} 🎂", next, false, $"/pets/{p.Id}"));
                }
            }
        }

        var petIds = pets.Select(p => p.Id).ToList();
        var breeding = await _db.BreedingRecords.AsNoTracking()
            .Where(b => petIds.Contains(b.DamId) && b.OutcomeOn == null && b.ExpectedDueOn != null && b.ExpectedDueOn <= horizon)
            .Select(b => new { b.DamId, DamName = b.Dam.Name, b.ExpectedDueOn }).ToListAsync(ct);
        items.AddRange(breeding.Select(b => new UpcomingCareItemDto("breeding", b.DamId, b.DamName,
            "Expected birth", b.ExpectedDueOn!.Value, b.ExpectedDueOn < now, "/livestock")));

        var meds = await _db.PetMedications.AsNoTracking()
            .Where(m => petIds.Contains(m.PetId) && m.IsActive && m.EndDate != null && m.EndDate >= now.Date && m.EndDate <= horizon)
            .Select(m => new { m.PetId, PetName = m.Pet.Name, m.DrugName, m.EndDate }).ToListAsync(ct);
        items.AddRange(meds.Select(m => new UpcomingCareItemDto("medication", m.PetId, m.PetName,
            $"{m.DrugName} course ends", m.EndDate!.Value, false, $"/pets/{m.PetId}?tab=medications")));

        return items.OrderBy(i => i.DueOn).ToList();
    }

    // ---------- Lineage ----------

    public async Task<LineageDto> GetLineageAsync(Guid petId, int generations, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        generations = Math.Clamp(generations, 1, 5);

        var nodes = new Dictionary<Guid, (Guid Id, string Name, string? Breed, Gender Gender, DateTime? BirthDate, Guid? SireId, Guid? DamId)>();
        var frontier = new List<Guid> { petId };
        for (var g = 0; g <= generations && frontier.Count > 0; g++)
        {
            var batch = await _db.Pets.AsNoTracking().Where(p => frontier.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Breed, p.Gender, p.BirthDate, p.SireId, p.DamId }).ToListAsync(ct);
            frontier = new List<Guid>();
            foreach (var p in batch)
            {
                if (!nodes.TryAdd(p.Id, (p.Id, p.Name, p.Breed, p.Gender, p.BirthDate, p.SireId, p.DamId))) continue;
                if (g == generations) continue;
                if (p.SireId is { } s && !nodes.ContainsKey(s)) frontier.Add(s);
                if (p.DamId is { } d && !nodes.ContainsKey(d)) frontier.Add(d);
            }
        }

        LineageNodeDto? Build(Guid? id, int depth, HashSet<Guid> path)
        {
            if (id is null || depth > generations || !nodes.TryGetValue(id.Value, out var n) || !path.Add(id.Value)) return null;
            var node = new LineageNodeDto(n.Id, n.Name, n.Breed, n.Gender, n.BirthDate,
                Build(n.SireId, depth + 1, new HashSet<Guid>(path)), Build(n.DamId, depth + 1, new HashSet<Guid>(path)));
            return node;
        }

        var offspring = await _db.Pets.AsNoTracking()
            .Where(p => p.SireId == petId || p.DamId == petId)
            .OrderBy(p => p.BirthDate)
            .Select(p => new PetRefDto(p.Id, p.Name, p.Breed, p.PrimaryPhotoUrl)).ToListAsync(ct);

        return new LineageDto(Build(petId, 0, new HashSet<Guid>())!, offspring);
    }

    // ---------- helpers ----------

    /// <summary>Latest record per vaccine name — older doses are superseded by the newer one.</summary>
    internal static IEnumerable<(string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn)> LatestPerVaccine(
        IEnumerable<(string VaccineName, DateTime AdministeredOn, DateTime? NextDueOn)> rows) =>
        rows.GroupBy(r => r.VaccineName.Trim().ToLowerInvariant())
            .Select(g => g.OrderByDescending(r => r.AdministeredOn).First());

    private static PetAccessLevel ToLevel(CaretakerRole? role) => role switch
    {
        null => PetAccessLevel.Owner,
        CaretakerRole.CoOwner => PetAccessLevel.CoOwner,
        CaretakerRole.Caretaker => PetAccessLevel.Caretaker,
        _ => PetAccessLevel.Viewer
    };

    internal static VaccinationDto ToDto(VaccinationRecord v) =>
        new(v.Id, v.VaccineName, v.AdministeredOn, v.NextDueOn, v.AdministeredByVet, v.Notes, v.CertificateUrl);

    internal static MedicalRecordDto ToDto(MedicalRecord m) =>
        new(m.Id, m.AppointmentId, m.VisitDate, m.Diagnosis, m.Treatment, m.PrescriptionFileUrl, m.Notes, m.VetName, m.AttachmentUrl);

    internal static GroomingRecordDto ToDto(GroomingRecord g) =>
        new(g.Id, g.PerformedOn, g.ServiceType, g.Provider, g.Notes, g.Cost, g.ServiceBookingId);

    internal static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Npgsql only accepts UTC DateTimes for timestamptz. Date-only inputs arrive Unspecified.</summary>
    internal static DateTime Utc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
    };
}
