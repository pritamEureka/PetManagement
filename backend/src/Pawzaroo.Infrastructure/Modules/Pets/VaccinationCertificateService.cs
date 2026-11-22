using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class VaccinationCertificateService : IVaccinationCertificateService
{
    public const string Notice = "Issued from records held on Pawzaroo. Verify the vaccine details with the issuing clinic; this is not an official government document.";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;

    public VaccinationCertificateService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access)
    { _db = db; _current = current; _access = access; }

    internal static string ContentHash(IEnumerable<VaccinationRecord> vaccs)
    {
        var sb = new StringBuilder();
        foreach (var v in vaccs.OrderBy(v => v.AdministeredOn).ThenBy(v => v.VaccineName, StringComparer.Ordinal))
            sb.Append(v.VaccineName).Append('|').Append(SealHasher.D(v.AdministeredOn)).Append('|').Append(SealHasher.D(v.NextDueOn)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public async Task<CertificateIssueDto> IssueAsync(Guid petId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var vaccs = await _db.VaccinationRecords.AsNoTracking().Where(v => v.PetId == petId).ToListAsync(ct);
        if (vaccs.Count == 0) throw new ConflictException("Record at least one vaccination before issuing a certificate.");
        var c = new VaccinationCertificate
        {
            PetId = petId, IssuedByUserId = uid, ContentHash = ContentHash(vaccs),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=')
        };
        _db.Set<VaccinationCertificate>().Add(c);
        await _db.SaveChangesAsync(ct);
        return new CertificateIssueDto(c.Id, c.Token, $"/v1/public/vaccination-certificates/{c.Token}",
            $"/v1/public/vaccination-certificates/{c.Token}/certificate", c.IssuedAt);
    }

    public async Task RevokeAsync(Guid petId, Guid certificateId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        var c = await _db.Set<VaccinationCertificate>().SingleOrDefaultAsync(x => x.Id == certificateId && x.PetId == petId, ct)
            ?? throw new NotFoundException("VaccinationCertificate", certificateId);
        c.RevokedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CertificateVerifyDto> VerifyAsync(string token, CancellationToken ct = default)
    {
        var empty = new CertificateVerifyDto(false, "not_found", null, null, null, null, null, null, false, Array.Empty<CertificateVaccineDto>(), Notice);
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return empty;
        var c = await _db.Set<VaccinationCertificate>().AsNoTracking().SingleOrDefaultAsync(x => x.Token == token, ct);
        if (c is null) return empty;
        if (c.RevokedAt is not null) return empty with { Status = "revoked" };

        var pet = await _db.Pets.AsNoTracking().Where(p => p.Id == c.PetId)
            .Select(p => new { p.Name, p.AnimalType, p.Breed, p.MicrochipNumber, Owner = p.Owner.DisplayName }).SingleOrDefaultAsync(ct);
        if (pet is null) return empty;
        var vaccs = await _db.VaccinationRecords.AsNoTracking().Where(v => v.PetId == c.PetId).OrderBy(v => v.AdministeredOn).ToListAsync(ct);
        var sealedIds = await _db.Set<HealthRecordSeal>().AsNoTracking()
            .Where(s => s.PetId == c.PetId && s.RecordType == HealthRecordType.Vaccination).Select(s => s.RecordId).ToListAsync(ct);
        var changed = ContentHash(vaccs) != c.ContentHash;
        return new CertificateVerifyDto(true, changed ? "valid_records_changed_since_issue" : "valid", pet.Name, pet.AnimalType.ToString(), pet.Breed,
            pet.MicrochipNumber, pet.Owner, c.IssuedAt, changed,
            vaccs.Select(v => new CertificateVaccineDto(v.VaccineName, v.AdministeredOn, v.NextDueOn, v.AdministeredByVet, sealedIds.Contains(v.Id))).ToList(), Notice);
    }
}

public class FhirExportService : IFhirExportService
{
    private readonly ApplicationDbContext _db;
    private readonly IPetAccessService _access;
    public FhirExportService(ApplicationDbContext db, IPetAccessService access) { _db = db; _access = access; }

    private static string Iso(DateTime d) => d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

    public async Task<string> ExportAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var p = await _db.Pets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == petId, ct) ?? throw new NotFoundException("Pet", petId);
        var vaccs = await _db.VaccinationRecords.AsNoTracking().Where(v => v.PetId == petId).ToListAsync(ct);
        var meds = await _db.PetMedications.AsNoTracking().Where(m => m.PetId == petId).ToListAsync(ct);
        var weights = await _db.PetWeightLogs.AsNoTracking().Where(w => w.PetId == petId).OrderBy(w => w.RecordedOn).ToListAsync(ct);
        var allergies = await _db.Set<PetAllergy>().AsNoTracking().Where(a => a.PetId == petId).ToListAsync(ct);

        var patientRef = $"Patient/{p.Id}";
        var entries = new JsonArray();
        void Add(string type, Guid id, JsonObject res)
        {
            res["resourceType"] = type; res["id"] = id.ToString();
            entries.Add(new JsonObject { ["fullUrl"] = $"urn:uuid:{id}", ["resource"] = res });
        }

        var patient = new JsonObject
        {
            ["name"] = new JsonArray(new JsonObject { ["text"] = p.Name }),
            ["gender"] = p.Gender switch { Gender.Male => "male", Gender.Female => "female", _ => "unknown" },
            ["extension"] = new JsonArray(
                new JsonObject { ["url"] = "http://hl7.org/fhir/StructureDefinition/patient-animal", ["extension"] = new JsonArray(
                    new JsonObject { ["url"] = "species", ["valueCodeableConcept"] = new JsonObject { ["text"] = p.AnimalType.ToString() } },
                    new JsonObject { ["url"] = "breed", ["valueCodeableConcept"] = new JsonObject { ["text"] = p.Breed ?? "unknown" } }) })
        };
        if (p.BirthDate is { } bd) patient["birthDate"] = bd.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(p.MicrochipNumber))
            patient["identifier"] = new JsonArray(new JsonObject { ["system"] = "urn:pawzaroo:microchip", ["value"] = p.MicrochipNumber });
        Add("Patient", p.Id, patient);

        foreach (var v in vaccs)
        {
            var im = new JsonObject
            {
                ["status"] = "completed", ["vaccineCode"] = new JsonObject { ["text"] = v.VaccineName },
                ["patient"] = new JsonObject { ["reference"] = patientRef }, ["occurrenceDateTime"] = Iso(v.AdministeredOn)
            };
            if (!string.IsNullOrWhiteSpace(v.AdministeredByVet)) im["performer"] = new JsonArray(new JsonObject { ["actor"] = new JsonObject { ["display"] = v.AdministeredByVet } });
            if (!string.IsNullOrWhiteSpace(v.Notes)) im["note"] = new JsonArray(new JsonObject { ["text"] = v.Notes });
            Add("Immunization", v.Id, im);
        }
        foreach (var m in meds)
        {
            var ms = new JsonObject
            {
                ["status"] = m.IsActive ? "active" : "stopped", ["medicationCodeableConcept"] = new JsonObject { ["text"] = m.DrugName },
                ["subject"] = new JsonObject { ["reference"] = patientRef },
                ["effectivePeriod"] = new JsonObject { ["start"] = Iso(m.StartDate) }
            };
            if (m.EndDate is { } e) ((JsonObject)ms["effectivePeriod"]!)["end"] = Iso(e);
            if (!string.IsNullOrWhiteSpace(m.Dose)) ms["dosage"] = new JsonArray(new JsonObject { ["text"] = $"{m.Dose} at {m.DoseTimes}" });
            Add("MedicationStatement", m.Id, ms);
        }
        foreach (var w in weights)
            Add("Observation", w.Id, new JsonObject
            {
                ["status"] = "final", ["code"] = new JsonObject { ["coding"] = new JsonArray(new JsonObject { ["system"] = "http://loinc.org", ["code"] = "29463-7", ["display"] = "Body weight" }) },
                ["subject"] = new JsonObject { ["reference"] = patientRef }, ["effectiveDateTime"] = Iso(w.RecordedOn),
                ["valueQuantity"] = new JsonObject { ["value"] = w.WeightKg, ["unit"] = "kg", ["system"] = "http://unitsofmeasure.org", ["code"] = "kg" }
            });
        foreach (var a in allergies)
            Add("AllergyIntolerance", a.Id, new JsonObject
            {
                ["clinicalStatus"] = new JsonObject { ["coding"] = new JsonArray(new JsonObject { ["system"] = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", ["code"] = a.Active ? "active" : "inactive" }) },
                ["category"] = new JsonArray(a.Category switch { AllergyCategory.Food => "food", AllergyCategory.Drug => "medication", AllergyCategory.Environmental => "environment", _ => "biologic" }),
                ["criticality"] = a.Severity >= AllergySeverity.Severe ? "high" : "low",
                ["code"] = new JsonObject { ["text"] = a.Allergen }, ["patient"] = new JsonObject { ["reference"] = patientRef },
                ["recordedDate"] = Iso(a.NotedAt),
                ["reaction"] = string.IsNullOrWhiteSpace(a.Reaction) ? new JsonArray() : new JsonArray(new JsonObject { ["manifestation"] = new JsonArray(new JsonObject { ["text"] = a.Reaction }) })
            });

        var bundle = new JsonObject { ["resourceType"] = "Bundle", ["type"] = "collection", ["timestamp"] = Iso(DateTime.UtcNow), ["entry"] = entries };
        return bundle.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}
