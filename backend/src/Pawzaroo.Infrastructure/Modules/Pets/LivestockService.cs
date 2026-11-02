using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>Herd / flock grouping, production logs and breeding records. Owner-scoped.</summary>
public class LivestockService : ILivestockService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public LivestockService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // ---------- Groups ----------

    public async Task<IReadOnlyList<PetGroupDto>> ListGroupsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.PetGroups.AsNoTracking().Where(g => g.OwnerId == uid)
            .OrderBy(g => g.Name)
            .Select(g => new PetGroupDto(g.Id, g.Name, g.AnimalType, g.Purpose, g.Notes, g.Members.Count,
                g.Members.OrderBy(m => m.Name).Select(m => new PetRefDto(m.Id, m.Name, m.Breed, m.PrimaryPhotoUrl)).ToList()))
            .ToListAsync(ct);
    }

    public async Task<PetGroupDto> CreateGroupAsync(PetGroupInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var g = new PetGroup { OwnerId = uid };
        Apply(g, input);
        _db.PetGroups.Add(g);
        await _db.SaveChangesAsync(ct);
        return new PetGroupDto(g.Id, g.Name, g.AnimalType, g.Purpose, g.Notes, 0, Array.Empty<PetRefDto>());
    }

    public async Task UpdateGroupAsync(Guid groupId, PetGroupInput input, CancellationToken ct = default)
    {
        var g = await OwnGroupAsync(groupId, ct);
        Apply(g, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        var g = await OwnGroupAsync(groupId, ct);
        await _db.Pets.Where(p => p.GroupId == groupId).ExecuteUpdateAsync(s => s.SetProperty(p => p.GroupId, (Guid?)null), ct);
        _db.PetGroups.Remove(g);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetMembersAsync(Guid groupId, GroupMembersInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var g = await OwnGroupAsync(groupId, ct);
        var ids = input.PetIds.Distinct().ToList();
        var owned = await _db.Pets.Where(p => ids.Contains(p.Id) && p.OwnerId == uid).ToListAsync(ct);
        if (owned.Count != ids.Count) throw new ForbiddenException("You can only group animals you own.");

        await _db.Pets.Where(p => p.GroupId == groupId && !ids.Contains(p.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.GroupId, (Guid?)null), ct);
        foreach (var p in owned) p.GroupId = g.Id;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> BulkVaccinateAsync(Guid groupId, VaccinationInput input, CancellationToken ct = default)
    {
        await OwnGroupAsync(groupId, ct);
        var petIds = await _db.Pets.Where(p => p.GroupId == groupId).Select(p => p.Id).ToListAsync(ct);
        if (petIds.Count == 0) throw new ConflictException("This group has no animals yet.");
        foreach (var id in petIds)
        {
            _db.VaccinationRecords.Add(new VaccinationRecord
            {
                PetId = id,
                VaccineName = input.VaccineName.Trim(),
                AdministeredOn = Utc(input.AdministeredOn),
                NextDueOn = input.NextDueOn.HasValue ? Utc(input.NextDueOn.Value) : null,
                AdministeredByVet = Clean(input.AdministeredByVet),
                Notes = Clean(input.Notes),
                CertificateUrl = Clean(input.CertificateUrl)
            });
        }
        await _db.SaveChangesAsync(ct);
        return petIds.Count;
    }

    private async Task<PetGroup> OwnGroupAsync(Guid groupId, CancellationToken ct)
    {
        var uid = Uid();
        return await _db.PetGroups.SingleOrDefaultAsync(g => g.Id == groupId && g.OwnerId == uid, ct)
            ?? throw new NotFoundException("Group", groupId);
    }

    private static void Apply(PetGroup g, PetGroupInput i)
    {
        g.Name = i.Name.Trim();
        g.AnimalType = i.AnimalType;
        g.Purpose = Clean(i.Purpose);
        g.Notes = Clean(i.Notes);
    }

    // ---------- Production ----------

    public async Task<IReadOnlyList<ProductionLogDto>> ListProductionAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = _db.ProductionLogs.AsNoTracking().Where(l => l.OwnerId == uid);
        if (from is { } f) q = q.Where(l => l.RecordedOn >= Utc(f));
        if (to is { } t) q = q.Where(l => l.RecordedOn <= Utc(t));
        return await q.OrderByDescending(l => l.RecordedOn).Take(500)
            .Select(l => new ProductionLogDto(l.Id, l.PetGroupId, l.PetGroup != null ? l.PetGroup.Name : null,
                l.PetId, l.Pet != null ? l.Pet.Name : null, l.Type, l.Quantity, l.Unit, l.RecordedOn, l.Notes))
            .ToListAsync(ct);
    }

    public async Task<ProductionLogDto> AddProductionAsync(ProductionLogInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        string? groupName = null, petName = null;
        if (input.PetGroupId is { } gid)
            groupName = await _db.PetGroups.Where(g => g.Id == gid && g.OwnerId == uid).Select(g => g.Name).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Group", gid);
        if (input.PetId is { } pid)
            petName = await _db.Pets.Where(p => p.Id == pid && p.OwnerId == uid).Select(p => p.Name).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Pet", pid);

        var l = new ProductionLog
        {
            OwnerId = uid, PetGroupId = input.PetGroupId, PetId = input.PetId, Type = input.Type,
            Quantity = input.Quantity, Unit = input.Unit.Trim(), RecordedOn = Utc(input.RecordedOn), Notes = Clean(input.Notes)
        };
        _db.ProductionLogs.Add(l);
        await _db.SaveChangesAsync(ct);
        return new ProductionLogDto(l.Id, l.PetGroupId, groupName, l.PetId, petName, l.Type, l.Quantity, l.Unit, l.RecordedOn, l.Notes);
    }

    public async Task DeleteProductionAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var l = await _db.ProductionLogs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct)
            ?? throw new NotFoundException("ProductionLog", id);
        _db.ProductionLogs.Remove(l);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Breeding ----------

    public async Task<IReadOnlyList<BreedingRecordDto>> ListBreedingAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.BreedingRecords.AsNoTracking().Where(b => b.OwnerId == uid)
            .OrderByDescending(b => b.MatedOn)
            .Select(b => new BreedingRecordDto(b.Id, b.DamId, b.Dam.Name, b.SireId, b.Sire != null ? b.Sire.Name : null,
                b.SireDescription, b.MatedOn, b.ExpectedDueOn, b.OutcomeOn, b.OffspringCount, b.Notes))
            .ToListAsync(ct);
    }

    public async Task<BreedingRecordDto> AddBreedingAsync(BreedingRecordInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var b = new BreedingRecord { OwnerId = uid };
        await ApplyAsync(b, input, uid, ct);
        _db.BreedingRecords.Add(b);
        await _db.SaveChangesAsync(ct);
        return (await ListBreedingAsync(ct)).Single(x => x.Id == b.Id);
    }

    public async Task UpdateBreedingAsync(Guid id, BreedingRecordInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var b = await _db.BreedingRecords.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct)
            ?? throw new NotFoundException("BreedingRecord", id);
        await ApplyAsync(b, input, uid, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteBreedingAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var b = await _db.BreedingRecords.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct)
            ?? throw new NotFoundException("BreedingRecord", id);
        _db.BreedingRecords.Remove(b);
        await _db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(BreedingRecord b, BreedingRecordInput i, Guid uid, CancellationToken ct)
    {
        var dam = await _db.Pets.AsNoTracking().Where(p => p.Id == i.DamId && p.OwnerId == uid)
            .Select(p => new { p.AnimalType, p.Gender }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Dam", i.DamId);
        if (dam.Gender == Domain.Common.Gender.Male) throw new ConflictException("The dam must be female.");
        if (i.SireId is { } sid && !await _db.Pets.AnyAsync(p => p.Id == sid && (p.OwnerId == uid || p.PublicProfileEnabled), ct))
            throw new NotFoundException("Sire", sid);

        b.DamId = i.DamId;
        b.SireId = i.SireId;
        b.SireDescription = Clean(i.SireDescription);
        b.MatedOn = Utc(i.MatedOn.Date);
        b.ExpectedDueOn = i.ExpectedDueOn.HasValue
            ? Utc(i.ExpectedDueOn.Value.Date)
            : PetKnowledge.GestationDays(dam.AnimalType) is { } days ? b.MatedOn.AddDays(days) : null;
        b.OutcomeOn = i.OutcomeOn.HasValue ? Utc(i.OutcomeOn.Value.Date) : null;
        b.OffspringCount = i.OffspringCount;
        b.Notes = Clean(i.Notes);
    }
}
