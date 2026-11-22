using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>No OCR engine ships with the platform. Swap this registration for a real implementation when one is available.</summary>
public sealed class NoOpDocumentOcr : IDocumentOcr
{
    public Task<OcrResult> ExtractTextAsync(string documentUrl, CancellationToken ct = default)
        => Task.FromResult(OcrResult.Unavailable);
}

public class PetHealthRecordsService : IPetHealthRecordsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IDocumentOcr _ocr;

    public PetHealthRecordsService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access, IDocumentOcr ocr)
    {
        _db = db; _current = current; _access = access; _ocr = ocr;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static ValidationException Invalid(string field, string msg)
        => new(new Dictionary<string, string[]> { [field] = new[] { msg } });

    private static string Like(string q) => "%" + q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    // ---------- Allergies ----------

    public async Task<IReadOnlyList<AllergyDto>> ListAllergiesAsync(Guid petId, string? q, bool? active, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var query = _db.Set<PetAllergy>().AsNoTracking().Where(a => a.PetId == petId);
        if (active is { } act) query = query.Where(a => a.Active == act);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = Like(q);
            query = query.Where(a => EF.Functions.ILike(a.Allergen, like) || (a.Reaction != null && EF.Functions.ILike(a.Reaction, like)));
        }
        return await query.OrderByDescending(a => a.Active).ThenByDescending(a => a.Severity).ThenBy(a => a.Allergen)
            .Select(a => new AllergyDto(a.Id, a.PetId, null, a.Allergen, a.Category, a.Severity, a.Reaction, a.NotedAt, a.Active))
            .Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AllergyDto>> SearchMyAllergiesAsync(string q, CancellationToken ct = default)
    {
        var uid = Uid();
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Array.Empty<AllergyDto>();
        var like = Like(q);
        var petIds = PetAccessService.AccessiblePets(_db, uid).Select(p => p.Id);
        return await _db.Set<PetAllergy>().AsNoTracking()
            .Where(a => petIds.Contains(a.PetId) && EF.Functions.ILike(a.Allergen, like))
            .OrderBy(a => a.Allergen)
            .Select(a => new AllergyDto(a.Id, a.PetId, a.Pet.Name, a.Allergen, a.Category, a.Severity, a.Reaction, a.NotedAt, a.Active))
            .Take(100).ToListAsync(ct);
    }

    public async Task<AllergyDto> AddAllergyAsync(Guid petId, AllergyInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var a = new PetAllergy { PetId = petId };
        ApplyAllergy(a, input);
        _db.Set<PetAllergy>().Add(a);
        await _db.SaveChangesAsync(ct);
        return ToDto(a);
    }

    public async Task UpdateAllergyAsync(Guid petId, Guid id, AllergyInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var a = await _db.Set<PetAllergy>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("Allergy", id);
        ApplyAllergy(a, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAllergyAsync(Guid petId, Guid id, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var a = await _db.Set<PetAllergy>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("Allergy", id);
        _db.Set<PetAllergy>().Remove(a);
        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyAllergy(PetAllergy a, AllergyInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Allergen)) throw Invalid("allergen", "Allergen is required.");
        if (i.Allergen.Trim().Length > 200) throw Invalid("allergen", "Allergen is too long.");
        if (!Enum.IsDefined(i.Category)) throw Invalid("category", "Unknown category.");
        if (!Enum.IsDefined(i.Severity)) throw Invalid("severity", "Unknown severity.");
        a.Allergen = i.Allergen.Trim();
        a.Category = i.Category;
        a.Severity = i.Severity;
        a.Reaction = Clean(i.Reaction);
        a.NotedAt = i.NotedAt.HasValue ? Utc(i.NotedAt.Value) : (a.NotedAt == default ? DateTime.UtcNow : a.NotedAt);
        a.Active = i.Active;
    }

    private static AllergyDto ToDto(PetAllergy a) => new(a.Id, a.PetId, null, a.Allergen, a.Category, a.Severity, a.Reaction, a.NotedAt, a.Active);

    // ---------- Diet ----------

    public async Task<IReadOnlyList<DietEntryDto>> ListDietAsync(Guid petId, string? q, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var query = _db.Set<PetDietEntry>().AsNoTracking().Where(d => d.PetId == petId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = Like(q);
            query = query.Where(d => EF.Functions.ILike(d.Food, like) || (d.Notes != null && EF.Functions.ILike(d.Notes, like)));
        }
        return await query.OrderByDescending(d => d.Active).ThenBy(d => d.Food)
            .Select(d => new DietEntryDto(d.Id, d.PetId, d.Food, d.Amount, d.Frequency, d.Notes, d.Active)).Take(200).ToListAsync(ct);
    }

    public async Task<DietEntryDto> AddDietAsync(Guid petId, DietEntryInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var d = new PetDietEntry { PetId = petId };
        ApplyDiet(d, input);
        _db.Set<PetDietEntry>().Add(d);
        await _db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task UpdateDietAsync(Guid petId, Guid id, DietEntryInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var d = await _db.Set<PetDietEntry>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("DietEntry", id);
        ApplyDiet(d, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteDietAsync(Guid petId, Guid id, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var d = await _db.Set<PetDietEntry>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("DietEntry", id);
        _db.Set<PetDietEntry>().Remove(d);
        await _db.SaveChangesAsync(ct);
    }

    private static void ApplyDiet(PetDietEntry d, DietEntryInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Food)) throw Invalid("food", "Food or brand is required.");
        d.Food = i.Food.Trim();
        d.Amount = Clean(i.Amount);
        d.Frequency = Clean(i.Frequency);
        d.Notes = Clean(i.Notes);
        d.Active = i.Active;
    }

    private static DietEntryDto ToDto(PetDietEntry d) => new(d.Id, d.PetId, d.Food, d.Amount, d.Frequency, d.Notes, d.Active);

    // ---------- Import from legacy text ----------

    public async Task<ImportFromTextResult> ImportFromTextAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == petId).Select(p => new { p.Allergies, p.DietNotes }).SingleAsync(ct);

        var existingAllergens = (await _db.Set<PetAllergy>().AsNoTracking().Where(a => a.PetId == petId).Select(a => a.Allergen).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingFood = (await _db.Set<PetDietEntry>().AsNoTracking().Where(d => d.PetId == petId).Select(d => d.Food).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var skipped = 0;
        var newAllergies = new List<PetAllergy>();
        foreach (var p in HealthTextParser.ParseAllergies(pet.Allergies))
        {
            if (!existingAllergens.Add(p.Allergen)) { skipped++; continue; }
            newAllergies.Add(new PetAllergy { PetId = petId, Allergen = p.Allergen, Category = p.Category, Severity = AllergySeverity.Mild,
                Reaction = null, NotedAt = DateTime.UtcNow, Active = true });
        }
        var newDiet = new List<PetDietEntry>();
        foreach (var p in HealthTextParser.ParseDiet(pet.DietNotes))
        {
            if (!existingFood.Add(p.Food)) { skipped++; continue; }
            newDiet.Add(new PetDietEntry { PetId = petId, Food = p.Food, Amount = p.Amount, Frequency = p.Frequency, Active = true });
        }
        _db.Set<PetAllergy>().AddRange(newAllergies);
        _db.Set<PetDietEntry>().AddRange(newDiet);
        await _db.SaveChangesAsync(ct);   // original free text is left untouched
        return new ImportFromTextResult(newAllergies.Count, newDiet.Count, skipped,
            newAllergies.Select(ToDto).ToList(), newDiet.Select(ToDto).ToList());
    }

    // ---------- Lab results ----------

    public async Task<IReadOnlyList<LabResultDto>> ListLabsAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var rows = await _db.Set<PetLabResult>().AsNoTracking().Where(l => l.PetId == petId)
            .OrderByDescending(l => l.TakenAt).ThenBy(l => l.TestName).Take(300).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<LabResultDto> AddLabAsync(Guid petId, LabResultInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var l = new PetLabResult { PetId = petId };
        await ApplyLabAsync(l, input, isNew: true, ct);
        _db.Set<PetLabResult>().Add(l);
        await _db.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public async Task<LabResultDto> UpdateLabAsync(Guid petId, Guid id, LabResultInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var l = await _db.Set<PetLabResult>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("LabResult", id);
        await ApplyLabAsync(l, input, isNew: false, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(l);
    }

    public async Task DeleteLabAsync(Guid petId, Guid id, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var l = await _db.Set<PetLabResult>().SingleOrDefaultAsync(x => x.Id == id && x.PetId == petId, ct) ?? throw new NotFoundException("LabResult", id);
        _db.Set<PetLabResult>().Remove(l);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyLabAsync(PetLabResult l, LabResultInput i, bool isNew, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(i.TestName)) throw Invalid("testName", "Test name is required.");
        if (!string.IsNullOrWhiteSpace(i.DocumentUrl) && !Uri.TryCreate(i.DocumentUrl, UriKind.Absolute, out var u)
            || (Uri.TryCreate(i.DocumentUrl, UriKind.Absolute, out var u2) && u2.Scheme is not ("http" or "https")))
            throw Invalid("documentUrl", "Document URL must be an http(s) link.");

        l.TestName = i.TestName.Trim();
        l.Value = Clean(i.Value);
        l.Unit = Clean(i.Unit);
        l.ReferenceRange = Clean(i.ReferenceRange);
        l.TakenAt = Utc(i.TakenAt);
        var newDoc = Clean(i.DocumentUrl);
        var docChanged = !string.Equals(newDoc, l.DocumentUrl, StringComparison.Ordinal);
        l.DocumentUrl = newDoc;
        l.Flag = i.Flag ?? LabMath.ComputeFlag(l.Value, l.ReferenceRange);

        if (l.Value is not null)
        {
            // A person typed the numbers in — either from scratch, or transcribing an uploaded document.
            l.Status = newDoc is not null && (isNew ? false : l.Status is LabResultStatus.OcrUnavailable or LabResultStatus.Transcribed)
                ? LabResultStatus.Transcribed
                : (l.Status == LabResultStatus.OcrExtracted ? LabResultStatus.OcrExtracted : LabResultStatus.Entered);
            if (newDoc is not null && l.Status == LabResultStatus.Entered && !isNew) l.Status = LabResultStatus.Transcribed;
        }
        else if (newDoc is not null && (isNew || docChanged))
        {
            // Document without values: try OCR; never fabricate — if unavailable, flag for manual transcription.
            var r = await _ocr.ExtractTextAsync(newDoc, ct);
            if (r.Available && !string.IsNullOrWhiteSpace(r.Text))
            {
                l.OcrText = r.Text;
                l.Status = LabResultStatus.OcrExtracted;
            }
            else
            {
                l.OcrText = null;
                l.Status = LabResultStatus.OcrUnavailable;
            }
        }
        else if (isNew)
        {
            l.Status = LabResultStatus.Entered;
        }
    }

    private static LabResultDto ToDto(PetLabResult l) => new(
        l.Id, l.PetId, l.TestName, l.Value, l.Unit, l.ReferenceRange, l.Flag, l.TakenAt, l.DocumentUrl, l.OcrText, l.Status,
        l.Status == LabResultStatus.OcrUnavailable,
        l.Status == LabResultStatus.OcrUnavailable ? "ocr_unavailable: automatic text extraction is not available — please transcribe the values from the document."
            : l.Status == LabResultStatus.OcrExtracted ? "Text was extracted automatically; please verify the values."
            : null);
}
