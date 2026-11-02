using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Application.Modules.Pets.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.Pets;

/// <summary>
/// Expense tracking. Manual entries are merged with spend the platform already
/// knows about: paid vet appointments, paid service bookings and grooming
/// records with a cost. Only owners / co-owners see money.
/// </summary>
public class PetFinanceService : IPetFinanceService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPetAccessService _access;

    public PetFinanceService(ApplicationDbContext db, ICurrentUserService current, IPetAccessService access)
    {
        _db = db;
        _current = current;
        _access = access;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<ExpenseDto>> ListExpensesAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        return (await LoadAsync(new[] { petId }, null, ct)).OrderByDescending(e => e.SpentOn).ToList();
    }

    public async Task<ExpenseDto> AddExpenseAsync(Guid petId, ExpenseInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var e = new PetExpense
        {
            PetId = petId, Category = input.Category, Amount = input.Amount,
            SpentOn = Utc(input.SpentOn), Description = Clean(input.Description)
        };
        _db.PetExpenses.Add(e);
        await _db.SaveChangesAsync(ct);
        var petName = await _db.Pets.Where(p => p.Id == petId).Select(p => p.Name).SingleAsync(ct);
        return new ExpenseDto(e.Id, petId, petName, e.Category, e.Amount, e.SpentOn, e.Description, "manual");
    }

    public async Task DeleteExpenseAsync(Guid petId, Guid expenseId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var e = await _db.PetExpenses.SingleOrDefaultAsync(x => x.Id == expenseId && x.PetId == petId, ct)
            ?? throw new NotFoundException("Expense", expenseId);
        _db.PetExpenses.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<ExpenseSummaryDto> SummaryAsync(int months, Guid? petId, CancellationToken ct = default)
    {
        var uid = Uid();
        months = Math.Clamp(months, 1, 24);
        var now = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        List<Guid> petIds;
        if (petId is { } id)
        {
            await _access.EnsureCanEditAsync(id, ct);
            petIds = new List<Guid> { id };
        }
        else
        {
            petIds = await PetAccessService.AccessiblePets(_db, uid, PetAccessLevel.CoOwner).Select(p => p.Id).ToListAsync(ct);
        }

        var rows = await LoadAsync(petIds, from, ct);
        var monthKeys = Enumerable.Range(0, months).Select(i => from.AddMonths(i).ToString("yyyy-MM")).ToList();
        var monthRows = monthKeys.Select(k =>
        {
            var inMonth = rows.Where(r => r.SpentOn.ToString("yyyy-MM") == k).ToList();
            return new ExpenseMonthDto(k, inMonth.Sum(r => r.Amount),
                inMonth.GroupBy(r => r.Category.ToString()).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)));
        }).ToList();

        return new ExpenseSummaryDto(
            rows.Sum(r => r.Amount),
            monthRows,
            rows.GroupBy(r => r.Category.ToString()).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)),
            rows.GroupBy(r => r.PetName).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)),
            rows.OrderByDescending(r => r.SpentOn).Take(15).ToList());
    }

    private async Task<List<ExpenseDto>> LoadAsync(IReadOnlyCollection<Guid> petIds, DateTime? from, CancellationToken ct)
    {
        var since = from ?? new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var manual = await _db.PetExpenses.AsNoTracking()
            .Where(e => petIds.Contains(e.PetId) && e.SpentOn >= since)
            .Select(e => new ExpenseDto(e.Id, e.PetId, e.Pet.Name, e.Category, e.Amount, e.SpentOn, e.Description, "manual"))
            .ToListAsync(ct);

        var appts = await _db.Appointments.AsNoTracking()
            .Where(a => a.PetId != null && petIds.Contains(a.PetId.Value) && a.PaymentStatus == PaymentStatus.Paid && a.ScheduledAt >= since)
            .Select(a => new ExpenseDto(null, a.PetId!.Value, a.Pet!.Name, PetExpenseCategory.Vet, a.Amount, a.ScheduledAt,
                "Vet consultation · " + a.Doctor.User.DisplayName, "appointment"))
            .ToListAsync(ct);

        var bookings = await _db.ServiceBookings.AsNoTracking()
            .Where(b => b.PetId != null && petIds.Contains(b.PetId.Value) && b.PaymentStatus == PaymentStatus.Paid && b.ScheduledAt >= since)
            .Select(b => new
            {
                b.PetId, PetName = b.Pet!.Name, b.Amount, b.ScheduledAt, b.ServiceProvider.ProviderType, b.ServiceProvider.BusinessName
            }).ToListAsync(ct);

        var grooming = await _db.GroomingRecords.AsNoTracking()
            .Where(g => petIds.Contains(g.PetId) && g.Cost != null && g.Cost > 0 && g.ServiceBookingId == null && g.PerformedOn >= since)
            .Select(g => new ExpenseDto(null, g.PetId, g.Pet.Name, PetExpenseCategory.Grooming, g.Cost!.Value, g.PerformedOn,
                g.ServiceType, "grooming"))
            .ToListAsync(ct);

        return manual.Concat(appts).Concat(grooming)
            .Concat(bookings.Select(b => new ExpenseDto(null, b.PetId!.Value, b.PetName,
                b.ProviderType switch
                {
                    ServiceProviderType.Grooming => PetExpenseCategory.Grooming,
                    ServiceProviderType.Training => PetExpenseCategory.Training,
                    _ => PetExpenseCategory.Services
                },
                b.Amount, b.ScheduledAt, $"{b.ProviderType} · {b.BusinessName}", "service")))
            .ToList();
    }

    // ---------- Insurance ----------

    public async Task<IReadOnlyList<InsurancePolicyDto>> ListPoliciesAsync(Guid petId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var today = DateTime.UtcNow.Date;
        return await _db.PetInsurancePolicies.AsNoTracking().Where(p => p.PetId == petId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new InsurancePolicyDto(p.Id, p.Provider, p.PolicyNumber, p.CoverageSummary, p.CoverageLimit,
                p.Premium, p.StartDate, p.EndDate, p.DocumentUrl, p.StartDate <= today && (p.EndDate == null || p.EndDate >= today)))
            .ToListAsync(ct);
    }

    public async Task<InsurancePolicyDto> AddPolicyAsync(Guid petId, InsurancePolicyInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var p = new PetInsurancePolicy { PetId = petId };
        Apply(p, input);
        _db.PetInsurancePolicies.Add(p);
        await _db.SaveChangesAsync(ct);
        var today = DateTime.UtcNow.Date;
        return new InsurancePolicyDto(p.Id, p.Provider, p.PolicyNumber, p.CoverageSummary, p.CoverageLimit, p.Premium,
            p.StartDate, p.EndDate, p.DocumentUrl, p.StartDate <= today && (p.EndDate == null || p.EndDate >= today));
    }

    public async Task UpdatePolicyAsync(Guid petId, Guid policyId, InsurancePolicyInput input, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var p = await _db.PetInsurancePolicies.SingleOrDefaultAsync(x => x.Id == policyId && x.PetId == petId, ct)
            ?? throw new NotFoundException("InsurancePolicy", policyId);
        Apply(p, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePolicyAsync(Guid petId, Guid policyId, CancellationToken ct = default)
    {
        await _access.EnsureCanEditAsync(petId, ct);
        var p = await _db.PetInsurancePolicies.SingleOrDefaultAsync(x => x.Id == policyId && x.PetId == petId, ct)
            ?? throw new NotFoundException("InsurancePolicy", policyId);
        _db.PetInsurancePolicies.Remove(p);
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(PetInsurancePolicy p, InsurancePolicyInput i)
    {
        p.Provider = i.Provider.Trim();
        p.PolicyNumber = i.PolicyNumber.Trim();
        p.CoverageSummary = Clean(i.CoverageSummary);
        p.CoverageLimit = i.CoverageLimit;
        p.Premium = i.Premium;
        p.StartDate = Utc(i.StartDate.Date);
        p.EndDate = i.EndDate.HasValue ? Utc(i.EndDate.Value.Date) : null;
        p.DocumentUrl = Clean(i.DocumentUrl);
    }
}
