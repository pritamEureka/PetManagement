using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Pets.HealthPlus;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class CarePlanService : ICarePlanService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;

    public CarePlanService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access)
    {
        _db = db; _current = current; _access = access;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool IsVetEditor => _current.Permissions.Contains(Permissions.Vets.Edit);

    private static ValidationException Invalid(string field, string msg)
        => new(new Dictionary<string, string[]> { [field] = new[] { msg } });

    // ---------- Templates (vet-published) ----------

    public async Task<IReadOnlyList<CarePlanTemplateDto>> ListTemplatesAsync(AnimalType? species, bool mineOnly, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = _db.Set<CarePlanTemplate>().AsNoTracking().Include(t => t.Items).AsQueryable();
        // Everyone sees published templates; authors additionally see their own drafts.
        q = mineOnly ? q.Where(t => t.PublishedByUserId == uid) : q.Where(t => t.IsPublished || t.PublishedByUserId == uid);
        if (species is { } s) q = q.Where(t => t.Species == s);
        var rows = await q.OrderBy(t => t.Title).Take(200).ToListAsync(ct);
        return rows.Select(t => ToDto(t, uid)).ToList();
    }

    public async Task<CarePlanTemplateDto> SaveTemplateAsync(Guid? id, CarePlanTemplateInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (!IsVetEditor) throw new ForbiddenException("Only veterinarians can publish care plan templates.");
        if (string.IsNullOrWhiteSpace(input.Title)) throw Invalid("title", "Title is required.");
        if (input.Items is null || input.Items.Count == 0) throw Invalid("items", "Add at least one item.");
        if (input.Items.Count > 60) throw Invalid("items", "A template can have at most 60 items.");
        foreach (var i in input.Items)
        {
            if (string.IsNullOrWhiteSpace(i.Title)) throw Invalid("items", "Every item needs a title.");
            if (i.OffsetDays < 0 || i.OffsetDays > 3650) throw Invalid("items", "Offset must be between 0 and 3650 days.");
            if (i.RepeatEveryDays is { } r && (r < 1 || r > 3650)) throw Invalid("items", "Repeat interval must be between 1 and 3650 days.");
        }

        CarePlanTemplate t;
        if (id is { } tid)
        {
            t = await _db.Set<CarePlanTemplate>().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == tid, ct)
                ?? throw new NotFoundException("CarePlanTemplate", tid);
            if (t.PublishedByUserId != uid) throw new ForbiddenException("You can only edit your own templates.");
            _db.Set<CarePlanTemplateItem>().RemoveRange(t.Items);
            t.Items.Clear();
        }
        else
        {
            t = new CarePlanTemplate { PublishedByUserId = uid };
            _db.Set<CarePlanTemplate>().Add(t);
        }
        t.Title = input.Title.Trim();
        t.Species = input.Species;
        t.Breed = Clean(input.Breed);
        t.Description = Clean(input.Description);
        t.IsPublished = input.IsPublished;
        var order = 0;
        foreach (var i in input.Items)
            _db.Set<CarePlanTemplateItem>().Add(new CarePlanTemplateItem
            {
                Template = t, TemplateId = t.Id, Type = i.Type, Title = i.Title.Trim(), OffsetDays = i.OffsetDays,
                RepeatEveryDays = i.RepeatEveryDays, RepeatCount = i.RepeatEveryDays.HasValue ? i.RepeatCount : null,
                Notes = Clean(i.Notes), SortOrder = order++
            });
        await _db.SaveChangesAsync(ct);

        var fresh = await _db.Set<CarePlanTemplate>().AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == t.Id, ct);
        return ToDto(fresh, uid);
    }

    public async Task SetTemplatePublishedAsync(Guid id, bool published, CancellationToken ct = default)
    {
        var uid = Uid();
        if (!IsVetEditor) throw new ForbiddenException("Only veterinarians can publish care plan templates.");
        var t = await _db.Set<CarePlanTemplate>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("CarePlanTemplate", id);
        if (t.PublishedByUserId != uid) throw new ForbiddenException("You can only change your own templates.");
        t.IsPublished = published;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        if (!IsVetEditor) throw new ForbiddenException("Only veterinarians can manage care plan templates.");
        var t = await _db.Set<CarePlanTemplate>().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("CarePlanTemplate", id);
        if (t.PublishedByUserId != uid) throw new ForbiddenException("You can only delete your own templates.");
        _db.Set<CarePlanTemplate>().Remove(t);   // soft delete; adopted plans keep their generated items
        await _db.SaveChangesAsync(ct);
    }

    private static CarePlanTemplateDto ToDto(CarePlanTemplate t, Guid uid) => new(
        t.Id, t.Title, t.Species, t.Breed, t.Description, t.IsPublished, t.PublishedByUserId, t.PublishedByUserId == uid, t.CreatedAt,
        t.Items.OrderBy(i => i.SortOrder).Select(i => new CarePlanTemplateItemDto(i.Id, i.Type, i.Title, i.OffsetDays, i.RepeatEveryDays, i.RepeatCount, i.Notes)).ToList());

    // ---------- Plans ----------

    public async Task<IReadOnlyList<CarePlanDto>> ListPlansAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var plans = await _db.Set<PetCarePlan>().AsNoTracking().Include(p => p.Items)
            .Where(p => p.PetId == petId && p.Status != CarePlanStatus.Archived)
            .OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        var names = await UserNamesAsync(plans.SelectMany(p => p.Items).Select(i => i.AssignedUserId), ct);
        return plans.Select(p => ToDto(p, names)).ToList();
    }

    public async Task<CarePlanDto> CreateCustomPlanAsync(Guid petId, CustomPlanInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        if (string.IsNullOrWhiteSpace(input.Title)) throw Invalid("title", "Title is required.");
        if (input.Items is null || input.Items.Count == 0) throw Invalid("items", "Add at least one item.");
        if (input.Items.Count > 100) throw Invalid("items", "A plan can have at most 100 items.");
        if (input.Items.Any(i => string.IsNullOrWhiteSpace(i.Title))) throw Invalid("items", "Every item needs a title.");

        var plan = new PetCarePlan
        {
            PetId = petId, IsCustom = true, Title = input.Title.Trim(), StartDate = Utc((input.StartDate ?? DateTime.UtcNow).Date)
        };
        foreach (var i in input.Items)
        {
            await EnsureAssigneeAsync(petId, i.AssignedUserId, ct);
            plan.Items.Add(NewItem(petId, i.Type, i.Title.Trim(), Utc(i.DueDate.Date), Clean(i.Notes), i.AssignedUserId));
        }
        _db.Set<PetCarePlan>().Add(plan);
        await _db.SaveChangesAsync(ct);
        return await GetPlanAsync(plan.Id, ct);
    }

    public async Task<CarePlanDto> AdoptTemplateAsync(Guid petId, AdoptTemplateInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var t = await _db.Set<CarePlanTemplate>().AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == input.TemplateId && x.IsPublished, ct)
            ?? throw new NotFoundException("CarePlanTemplate", input.TemplateId);
        await EnsureAssigneeAsync(petId, input.AssignedUserId, ct);

        var start = Utc((input.StartDate ?? DateTime.UtcNow).Date);
        var generated = CarePlanMath.Generate(
            t.Items.OrderBy(i => i.SortOrder).Select(i => new CarePlanBlueprintItem(i.Type, i.Title, i.OffsetDays, i.RepeatEveryDays, i.RepeatCount, i.Notes)),
            start);

        var plan = new PetCarePlan { PetId = petId, TemplateId = t.Id, IsCustom = false, Title = t.Title, StartDate = start };
        foreach (var g in generated)
            plan.Items.Add(NewItem(petId, g.Type, g.Title, g.DueDate, g.Notes, input.AssignedUserId));
        _db.Set<PetCarePlan>().Add(plan);
        await _db.SaveChangesAsync(ct);
        return await GetPlanAsync(plan.Id, ct);
    }

    public async Task<CarePlanItemDto> AddItemAsync(Guid petId, Guid planId, CustomPlanItemInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        if (string.IsNullOrWhiteSpace(input.Title)) throw Invalid("title", "Title is required.");
        var plan = await _db.Set<PetCarePlan>().SingleOrDefaultAsync(p => p.Id == planId && p.PetId == petId, ct)
            ?? throw new NotFoundException("CarePlan", planId);
        await EnsureAssigneeAsync(petId, input.AssignedUserId, ct);
        var item = NewItem(petId, input.Type, input.Title.Trim(), Utc(input.DueDate.Date), Clean(input.Notes), input.AssignedUserId);
        item.PlanId = plan.Id;
        if (plan.Status == CarePlanStatus.Completed) plan.Status = CarePlanStatus.Active;
        _db.Set<PetCarePlanItem>().Add(item);
        await _db.SaveChangesAsync(ct);
        return await GetItemDtoAsync(item.Id, ct);
    }

    public async Task<CarePlanItemDto> CompleteItemAsync(Guid petId, Guid itemId, CompleteCareItemInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        await _access.EnsureCanLogCareAsync(petId, ct);
        var item = await LoadItemAsync(petId, itemId, ct);
        if (item.Status == CarePlanItemStatus.Done) throw new ConflictException("This item is already done.");

        var now = DateTime.UtcNow;
        if (input.CreateRecord)
        {
            // Clinical records need clinical access; caretakers can only tick the item.
            await _access.EnsureCanEditClinicalAsync(petId, ct);
            switch (item.Type)
            {
                case CarePlanItemType.Vaccination:
                    var v = new VaccinationRecord { PetId = petId, VaccineName = item.Title, AdministeredOn = now, Notes = Clean(input.Notes) ?? "From care plan" };
                    _db.VaccinationRecords.Add(v);
                    item.LinkedRecordType = "Vaccination"; item.LinkedRecordId = v.Id;
                    break;
                case CarePlanItemType.Grooming:
                    var g = new GroomingRecord { PetId = petId, PerformedOn = now, ServiceType = item.Title, Notes = Clean(input.Notes) };
                    _db.GroomingRecords.Add(g);
                    item.LinkedRecordType = "Grooming"; item.LinkedRecordId = g.Id;
                    break;
                default:
                    throw new ConflictException("Automatic record creation is only available for vaccination and grooming items.");
            }
        }
        else if (input.LinkedRecordId is { } lid)
        {
            item.LinkedRecordId = lid;
            item.LinkedRecordType = Clean(input.LinkedRecordType) is { Length: <= 32 } lt ? lt : item.Type.ToString();
        }

        item.Status = CarePlanItemStatus.Done;
        item.CompletedAt = now;
        item.CompletedByUserId = uid;
        if (Clean(input.Notes) is { } n) item.Notes = string.IsNullOrEmpty(item.Notes) ? n : $"{item.Notes}\n{n}";

        // Close the plan once nothing is outstanding.
        var plan = await _db.Set<PetCarePlan>().SingleAsync(p => p.Id == item.PlanId, ct);
        var otherOpen = await _db.Set<PetCarePlanItem>().AnyAsync(i => i.PlanId == plan.Id && i.Id != item.Id && i.Status != CarePlanItemStatus.Done, ct);
        if (!otherOpen) plan.Status = CarePlanStatus.Completed;

        await _db.SaveChangesAsync(ct);
        return await GetItemDtoAsync(item.Id, ct);
    }

    public async Task<CarePlanItemDto> RescheduleItemAsync(Guid petId, Guid itemId, RescheduleCareItemInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanLogCareAsync(petId, ct);
        var item = await LoadItemAsync(petId, itemId, ct);
        if (item.Status == CarePlanItemStatus.Done) throw new ConflictException("Completed items can't be rescheduled.");
        var newDate = Utc(input.NewDueDate.Date);
        if (newDate < DateTime.UtcNow.Date.AddDays(-1)) throw Invalid("newDueDate", "Pick today or a future date.");
        item.OriginalDueDate ??= item.DueDate;
        item.DueDate = newDate;
        item.Status = CarePlanItemStatus.Rescheduled;
        item.LastNotifiedStage = null;   // re-arm reminders
        if (Clean(input.Reason) is { } r) item.Notes = string.IsNullOrEmpty(item.Notes) ? $"Rescheduled: {r}" : $"{item.Notes}\nRescheduled: {r}";
        await _db.SaveChangesAsync(ct);
        return await GetItemDtoAsync(item.Id, ct);
    }

    public async Task<CarePlanItemDto> AssignItemAsync(Guid petId, Guid itemId, AssignCareItemInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var item = await LoadItemAsync(petId, itemId, ct);
        await EnsureAssigneeAsync(petId, input.UserId, ct);
        item.AssignedUserId = input.UserId;
        await _db.SaveChangesAsync(ct);
        return await GetItemDtoAsync(item.Id, ct);
    }

    public async Task ArchivePlanAsync(Guid petId, Guid planId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var plan = await _db.Set<PetCarePlan>().SingleOrDefaultAsync(p => p.Id == planId && p.PetId == petId, ct)
            ?? throw new NotFoundException("CarePlan", planId);
        plan.Status = CarePlanStatus.Archived;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MissedCareSummaryDto> MissedSummaryAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanReadAsync(petId, ct);
        var now = DateTime.UtcNow;
        var horizon = now.Date.AddDays(8);
        var open = await _db.Set<PetCarePlanItem>().AsNoTracking()
            .Where(i => i.PetId == petId && i.Status != CarePlanItemStatus.Done && i.Plan.Status == CarePlanStatus.Active && i.DueDate < horizon)
            .OrderBy(i => i.DueDate).Take(300).ToListAsync(ct);
        var names = await UserNamesAsync(open.Select(i => i.AssignedUserId), ct);
        var dtos = open.Select(i => ToDto(i, names, now)).ToList();
        var missed = dtos.Where(d => d.IsMissed).ToList();
        var soon = dtos.Where(d => !d.IsMissed).ToList();
        return new MissedCareSummaryDto(missed.Count, soon.Count, missed, soon);
    }

    // ---------- Helpers ----------

    private static PetCarePlanItem NewItem(Guid petId, CarePlanItemType type, string title, DateTime due, string? notes, Guid? assignee) => new()
    {
        PetId = petId, Type = type, Title = title, DueDate = due, Notes = notes, AssignedUserId = assignee
    };

    private async Task<PetCarePlanItem> LoadItemAsync(Guid petId, Guid itemId, CancellationToken ct)
        => await _db.Set<PetCarePlanItem>().SingleOrDefaultAsync(i => i.Id == itemId && i.PetId == petId, ct)
           ?? throw new NotFoundException("CarePlanItem", itemId);

    /// <summary>The assignee must be the owner or an active caretaker/co-owner of the pet.</summary>
    private async Task EnsureAssigneeAsync(Guid petId, Guid? userId, CancellationToken ct)
    {
        if (userId is not { } u) return;
        var now = DateTime.UtcNow;
        var ok = await _db.Pets.AnyAsync(p => p.Id == petId && p.OwnerId == u, ct)
              || await _db.PetCaretakers.AnyAsync(c => c.PetId == petId && c.UserId == u && c.Role >= CaretakerRole.Caretaker
                                                        && (c.ExpiresAt == null || c.ExpiresAt > now), ct);
        if (!ok) throw Invalid("assignedUserId", "The assignee must be the owner or a caretaker of this pet.");
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var list = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (list.Count == 0) return new();
        return await _db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    private async Task<CarePlanDto> GetPlanAsync(Guid planId, CancellationToken ct)
    {
        var p = await _db.Set<PetCarePlan>().AsNoTracking().Include(x => x.Items).SingleAsync(x => x.Id == planId, ct);
        var names = await UserNamesAsync(p.Items.Select(i => i.AssignedUserId), ct);
        return ToDto(p, names);
    }

    private async Task<CarePlanItemDto> GetItemDtoAsync(Guid itemId, CancellationToken ct)
    {
        var i = await _db.Set<PetCarePlanItem>().AsNoTracking().SingleAsync(x => x.Id == itemId, ct);
        var names = await UserNamesAsync(new Guid?[] { i.AssignedUserId }, ct);
        return ToDto(i, names, DateTime.UtcNow);
    }

    private static CarePlanItemDto ToDto(PetCarePlanItem i, IReadOnlyDictionary<Guid, string> names, DateTime now) => new(
        i.Id, i.PlanId, i.Type, i.Title, i.DueDate, i.OriginalDueDate, i.Status, CarePlanMath.IsMissed(i.Status, i.DueDate, now),
        i.CompletedAt, i.Notes, i.LinkedRecordType, i.LinkedRecordId, i.AssignedUserId,
        i.AssignedUserId is { } a && names.TryGetValue(a, out var n) ? n : null);

    private static CarePlanDto ToDto(PetCarePlan p, IReadOnlyDictionary<Guid, string> names)
    {
        var now = DateTime.UtcNow;
        var items = p.Items.OrderBy(i => i.DueDate).ThenBy(i => i.Title).Select(i => ToDto(i, names, now)).ToList();
        return new CarePlanDto(p.Id, p.PetId, p.TemplateId, p.IsCustom, p.Title, p.Status, p.StartDate,
            items.Count(i => !i.IsMissed && i.Status != CarePlanItemStatus.Done), items.Count(i => i.Status == CarePlanItemStatus.Done),
            items.Count(i => i.IsMissed), items);
    }
}
