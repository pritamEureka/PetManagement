using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>Owner-scoped herd diagnosis/treatment with meat/milk withdrawal tracking.</summary>
public class LivestockHealthService : ILivestockHealthService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public LivestockHealthService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static ValidationException Invalid(string f, string m) => new(new Dictionary<string, string[]> { [f] = new[] { m } });

    public async Task<LivestockTreatmentResultDto> AddTreatmentAsync(LivestockTreatmentInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (string.IsNullOrWhiteSpace(input.Diagnosis)) throw Invalid("diagnosis", "Diagnosis is required.");
        if (string.IsNullOrWhiteSpace(input.Treatment)) throw Invalid("treatment", "Treatment is required.");
        if (input.MeatWithdrawalDays is < 0 or > 730 || input.MilkWithdrawalDays is < 0 or > 730) throw Invalid("withdrawal", "Withdrawal days must be between 0 and 730.");
        if ((input.PetId is null) == (input.PetGroupId is null)) throw Invalid("target", "Choose either one animal or one group.");

        List<(Guid Id, string Name)> animals;
        if (input.PetGroupId is { } gid)
        {
            if (!await _db.PetGroups.AnyAsync(g => g.Id == gid && g.OwnerId == uid, ct)) throw new NotFoundException("Group", gid);
            animals = (await _db.Pets.AsNoTracking().Where(p => p.GroupId == gid && p.OwnerId == uid).Select(p => new { p.Id, p.Name }).ToListAsync(ct))
                .Select(p => (p.Id, p.Name)).ToList();
            if (animals.Count == 0) throw new ConflictException("This group has no animals yet.");
        }
        else
        {
            var pid = input.PetId!.Value;
            var p = await _db.Pets.AsNoTracking().Where(x => x.Id == pid && x.OwnerId == uid).Select(x => new { x.Id, x.Name }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Pet", pid);
            animals = new() { (p.Id, p.Name) };
        }

        var on = Utc(input.AdministeredOn);
        var batch = Guid.NewGuid();
        var rows = animals.Select(a => new LivestockTreatment
        {
            OwnerId = uid, PetId = a.Id, PetGroupId = input.PetGroupId, BatchId = batch,
            Diagnosis = input.Diagnosis.Trim(), Treatment = input.Treatment.Trim(), AdministeredOn = on,
            MeatWithdrawalDays = input.MeatWithdrawalDays, MilkWithdrawalDays = input.MilkWithdrawalDays,
            MeatSafeOn = WithdrawalMath.SafeOn(on, input.MeatWithdrawalDays), MilkSafeOn = WithdrawalMath.SafeOn(on, input.MilkWithdrawalDays),
            Notes = Clean(input.Notes)
        }).ToList();
        _db.Set<LivestockTreatment>().AddRange(rows);
        await _db.SaveChangesAsync(ct);   // single SaveChanges = one transaction for the whole herd
        var names = animals.ToDictionary(a => a.Id, a => a.Name);
        return new LivestockTreatmentResultDto(rows.Count, batch, rows.Select(r => ToDto(r, names[r.PetId])).ToList());
    }

    public async Task<IReadOnlyList<LivestockTreatmentDto>> ListTreatmentsAsync(Guid? groupId, Guid? petId, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = _db.Set<LivestockTreatment>().AsNoTracking().Where(t => t.OwnerId == uid);
        if (groupId is { } g) q = q.Where(t => t.PetGroupId == g);
        if (petId is { } p) q = q.Where(t => t.PetId == p);
        var rows = await q.OrderByDescending(t => t.AdministeredOn).Take(500).Select(t => new { T = t, Name = t.Pet.Name }).ToListAsync(ct);
        return rows.Select(r => ToDto(r.T, r.Name)).ToList();
    }

    public async Task<IReadOnlyList<WithdrawalStatusDto>> WithdrawalStatusAsync(Guid? groupId, CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var q = _db.Set<LivestockTreatment>().AsNoTracking().Where(t => t.OwnerId == uid && (t.MeatSafeOn > now || t.MilkSafeOn > now));
        if (groupId is { } g) q = q.Where(t => t.Pet.GroupId == g);
        var rows = await q.Select(t => new { t.PetId, Name = t.Pet.Name, GroupId = t.Pet.GroupId, t.MeatSafeOn, t.MilkSafeOn }).ToListAsync(ct);
        return rows.GroupBy(r => r.PetId).Select(grp =>
        {
            var meat = WithdrawalMath.Latest(grp.Select(x => x.MeatSafeOn));
            var milk = WithdrawalMath.Latest(grp.Select(x => x.MilkSafeOn));
            var f = grp.First();
            return new WithdrawalStatusDto(grp.Key, f.Name, f.GroupId, meat, milk, WithdrawalMath.InWithdrawal(meat, now), WithdrawalMath.InWithdrawal(milk, now));
        }).OrderBy(w => w.PetName).ToList();
    }

    public async Task DeleteTreatmentAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var t = await _db.Set<LivestockTreatment>().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct) ?? throw new NotFoundException("Treatment", id);
        _db.Set<LivestockTreatment>().Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    private static LivestockTreatmentDto ToDto(LivestockTreatment t, string name) => new(t.Id, t.BatchId, t.PetId, name, t.PetGroupId, t.Diagnosis,
        t.Treatment, t.AdministeredOn, t.MeatWithdrawalDays, t.MilkWithdrawalDays, t.MeatSafeOn, t.MilkSafeOn, t.Notes);
}
