using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using System.Text.Json;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>
/// Small additive hooks used by <see cref="PetService"/>'s edit paths: refuse edits/deletes of sealed
/// records and write a "before" version row (committed by the caller's SaveChanges).
/// </summary>
public static class HealthRecordGuard
{
    public const string SealedMessage =
        "This record has been sealed by the treating vet and can't be edited or deleted. Submit an amendment with a reason instead.";

    public static async Task EnsureNotSealedAsync(ApplicationDbContext db, HealthRecordType type, Guid recordId, CancellationToken ct)
    {
        if (await db.Set<HealthRecordSeal>().AnyAsync(s => s.RecordType == type && s.RecordId == recordId, ct))
            throw new ConflictException(SealedMessage);
    }

    public static Task AddVersionAsync(ApplicationDbContext db, Guid userId, VaccinationRecord before, string? reason, CancellationToken ct)
        => AddVersionAsync(db, userId, HealthRecordType.Vaccination, before.Id, SealHasher.Snapshot(before), reason, ct);

    public static Task AddVersionAsync(ApplicationDbContext db, Guid userId, MedicalRecord before, string? reason, CancellationToken ct)
        => AddVersionAsync(db, userId, HealthRecordType.Medical, before.Id, SealHasher.Snapshot(before), reason, ct);

    private static async Task AddVersionAsync(ApplicationDbContext db, Guid userId, HealthRecordType type, Guid recordId,
        Dictionary<string, string?> snapshot, string? reason, CancellationToken ct)
    {
        var last = await db.Set<HealthRecordVersion>().Where(v => v.RecordType == type && v.RecordId == recordId)
            .MaxAsync(v => (int?)v.Version, ct) ?? 0;
        db.Set<HealthRecordVersion>().Add(new HealthRecordVersion
        {
            RecordType = type, RecordId = recordId, Version = last + 1,
            SnapshotJson = SealHasher.Canonical(snapshot), ChangedByUserId = userId,
            ChangeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()
        });
    }
}

public class HealthRecordIntegrityService : IHealthRecordIntegrityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;
    private readonly IAuditLogger _audit;

    public HealthRecordIntegrityService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access, IAuditLogger audit)
    {
        _db = db; _current = current; _access = access; _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // Loads the live record's canonical snapshot (also proves the record belongs to the pet).
    private async Task<(Dictionary<string, string?> Snapshot, VaccinationRecord? Vac, MedicalRecord? Med)> LoadAsync(
        Guid petId, HealthRecordType type, Guid recordId, bool track, CancellationToken ct)
    {
        if (type == HealthRecordType.Vaccination)
        {
            IQueryable<VaccinationRecord> q = track ? _db.VaccinationRecords : _db.VaccinationRecords.AsNoTracking();
            var v = await q.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
                ?? throw new NotFoundException("Vaccination", recordId);
            return (SealHasher.Snapshot(v), v, null);
        }
        IQueryable<MedicalRecord> q2 = track ? _db.MedicalRecords : _db.MedicalRecords.AsNoTracking();
        var m = await q2.SingleOrDefaultAsync(x => x.Id == recordId && x.PetId == petId, ct)
            ?? throw new NotFoundException("MedicalRecord", recordId);
        return (SealHasher.Snapshot(m), null, m);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public async Task<RecordHistoryDto> HistoryAsync(Guid petId, HealthRecordType type, Guid recordId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var (snapshot, _, _) = await LoadAsync(petId, type, recordId, false, ct);

        var versions = await _db.Set<HealthRecordVersion>().AsNoTracking()
            .Where(v => v.RecordType == type && v.RecordId == recordId).OrderByDescending(v => v.Version).ToListAsync(ct);
        var seal = await _db.Set<HealthRecordSeal>().AsNoTracking().SingleOrDefaultAsync(s => s.RecordType == type && s.RecordId == recordId, ct);

        var ids = versions.Select(v => v.ChangedByUserId).ToList();
        if (seal is not null) ids.Add(seal.SealedByUserId);
        var names = await NamesAsync(ids, ct);

        RecordSealDto? sealDto = null;
        if (seal is not null)
        {
            var valid = SealHasher.Verify(seal.SnapshotJson, seal.SealedByUserId, seal.SealedAt, seal.SignatureHash);
            var changed = SealHasher.Canonical(snapshot) != seal.SnapshotJson;
            sealDto = new RecordSealDto(type, recordId, seal.SealedByUserId, names.GetValueOrDefault(seal.SealedByUserId), seal.VetDoctorId,
                seal.SealedAt, seal.SignatureHash, valid, changed);
        }

        return new RecordHistoryDto(type, recordId, seal is not null, sealDto,
            versions.Select(v => new RecordVersionDto(v.Version, v.CreatedAt, v.ChangedByUserId, names.GetValueOrDefault(v.ChangedByUserId),
                v.ChangeReason, JsonSerializer.Deserialize<Dictionary<string, string?>>(v.SnapshotJson) ?? new())).ToList());
    }

    public async Task<IReadOnlyList<SealStateDto>> ListSealsAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var seals = await _db.Set<HealthRecordSeal>().AsNoTracking().Where(s => s.PetId == petId).ToListAsync(ct);
        var names = await NamesAsync(seals.Select(s => s.SealedByUserId), ct);
        return seals.Select(s => new SealStateDto(s.RecordId, s.RecordType, s.SealedAt, names.GetValueOrDefault(s.SealedByUserId))).ToList();
    }

    public async Task<RecordSealDto> SealAsync(Guid petId, HealthRecordType type, Guid recordId, CancellationToken ct = default)
    {
        var uid = Uid();
        await _access.EnsureCanReadAsync(petId, ct);
        if (!_current.Permissions.Contains(Permissions.Vets.Edit))
            throw new ForbiddenException("Only a veterinarian can seal a health record.");
        // "Treating vet" = Vet access level (live appointment for this pet). Owners/caretakers never qualify.
        if (await _access.GetLevelAsync(petId, ct) != PetAccessLevel.Vet)
            throw new ForbiddenException("Only the treating vet of this pet can seal its records.");

        var (snapshot, _, _) = await LoadAsync(petId, type, recordId, false, ct);
        if (await _db.Set<HealthRecordSeal>().AnyAsync(s => s.RecordType == type && s.RecordId == recordId, ct))
            throw new ConflictException("This record is already sealed.");

        var doctorId = await _db.Doctors.AsNoTracking().Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).FirstOrDefaultAsync(ct);
        var canonical = SealHasher.Canonical(snapshot);
        var sealedAt = DateTime.UtcNow;
        var seal = new HealthRecordSeal
        {
            RecordType = type, RecordId = recordId, PetId = petId, SealedByUserId = uid, VetDoctorId = doctorId,
            SealedAt = sealedAt, SnapshotJson = canonical, SignatureHash = SealHasher.Hash(canonical, uid, sealedAt)
        };
        _db.Set<HealthRecordSeal>().Add(seal);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("health_record_sealed", type.ToString(), recordId.ToString(), "Pets",
            newValues: new { petId, seal.SignatureHash }, ct: ct);
        var name = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync(ct);
        return new RecordSealDto(type, recordId, uid, name, doctorId, sealedAt, seal.SignatureHash, true, false);
    }

    public async Task AmendAsync(Guid petId, HealthRecordType type, Guid recordId, AmendRecordInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await _access.EnsureCanEditClinicalAsync(petId, ct);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length < 5)
            throw new ValidationException(new Dictionary<string, string[]> { ["reason"] = new[] { "An amendment reason (at least 5 characters) is required." } });

        var (_, vac, med) = await LoadAsync(petId, type, recordId, true, ct);
        if (type == HealthRecordType.Vaccination && vac is not null)
        {
            var i = input.Vaccination ?? throw new ValidationException(new Dictionary<string, string[]> { ["vaccination"] = new[] { "Vaccination values are required." } });
            await HealthRecordGuard.AddVersionAsync(_db, uid, vac, input.Reason, ct);
            var dueChanged = vac.NextDueOn != i.NextDueOn;
            vac.VaccineName = i.VaccineName.Trim();
            vac.AdministeredOn = Utc(i.AdministeredOn);
            vac.NextDueOn = i.NextDueOn.HasValue ? Utc(i.NextDueOn.Value) : null;
            vac.AdministeredByVet = Clean(i.AdministeredByVet);
            vac.Notes = Clean(i.Notes);
            vac.CertificateUrl = Clean(i.CertificateUrl);
            if (dueChanged) vac.LastReminderStage = null;
        }
        else if (med is not null)
        {
            var i = input.Medical ?? throw new ValidationException(new Dictionary<string, string[]> { ["medical"] = new[] { "Medical record values are required." } });
            await HealthRecordGuard.AddVersionAsync(_db, uid, med, input.Reason, ct);
            med.VisitDate = Utc(i.VisitDate);
            med.Diagnosis = Clean(i.Diagnosis);
            med.Treatment = Clean(i.Treatment);
            med.Notes = Clean(i.Notes);
            med.VetName = Clean(i.VetName);
            med.AttachmentUrl = Clean(i.AttachmentUrl);
            med.PrescriptionFileUrl = Clean(i.PrescriptionFileUrl) ?? med.PrescriptionFileUrl;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("health_record_amended", type.ToString(), recordId.ToString(), "Pets",
            newValues: new { petId, reason = input.Reason }, ct: ct);
    }
}
