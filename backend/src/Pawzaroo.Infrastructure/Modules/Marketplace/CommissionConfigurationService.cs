using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class CommissionConfigurationService : ICommissionConfigurationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public CommissionConfigurationService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<IReadOnlyList<CommissionConfigurationDto>> ListAsync(CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.View)) throw new ForbiddenException();
        return await _db.CommissionConfigurations.AsNoTracking()
            .OrderByDescending(c => c.EffectiveFrom)
            .Select(c => new CommissionConfigurationDto(
                c.Id, c.Scope,
                c.StoreId, c.Store != null ? c.Store.Name : null,
                c.CategoryId, c.Category != null ? c.Category.Name : null,
                c.CommissionPercent, c.FlatFee, c.MinCommission, c.MaxCommission,
                c.EffectiveFrom, c.EffectiveTo, c.Notes))
            .ToListAsync(ct);
    }

    public async Task<Guid> UpsertAsync(UpsertCommissionConfigurationInput input, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.Edit)) throw new ForbiddenException();
        if (input.Scope == CommissionScope.Store && !input.StoreId.HasValue)
            throw new ValidationException(new Dictionary<string, string[]> { ["storeId"] = new[] { "Required for store-scope." } });
        if (input.Scope == CommissionScope.Category && !input.CategoryId.HasValue)
            throw new ValidationException(new Dictionary<string, string[]> { ["categoryId"] = new[] { "Required for category-scope." } });

        var entity = new CommissionConfiguration
        {
            Scope = input.Scope,
            StoreId = input.Scope == CommissionScope.Store ? input.StoreId : null,
            CategoryId = input.Scope == CommissionScope.Category ? input.CategoryId : null,
            CommissionPercent = input.CommissionPercent,
            FlatFee = input.FlatFee,
            MinCommission = input.MinCommission,
            MaxCommission = input.MaxCommission,
            EffectiveFrom = input.EffectiveFrom,
            EffectiveTo = input.EffectiveTo,
            Notes = input.Notes
        };
        _db.CommissionConfigurations.Add(entity);
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.AdminEvents,
            new CommissionConfigurationChanged(entity.Id, entity.Scope.ToString(),
                entity.StoreId, entity.CategoryId, entity.CommissionPercent, DateTime.UtcNow),
            entity.Id.ToString(), ct);
        return entity.Id;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.Edit)) throw new ForbiddenException();
        var c = await _db.CommissionConfigurations.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("CommissionConfiguration", id);
        c.IsDeleted = true;
        c.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Precedence: store override -&gt; category rule -&gt; global rule -&gt; store-row CommissionPercent fallback.
    /// </summary>
    public async Task<decimal> ResolveAsync(Guid storeId, Guid? categoryId, DateTime atUtc, CancellationToken ct = default)
    {
        var active = _db.CommissionConfigurations.AsNoTracking()
            .Where(c => c.EffectiveFrom <= atUtc && (c.EffectiveTo == null || c.EffectiveTo > atUtc));

        var storeRule = await active.Where(c => c.Scope == CommissionScope.Store && c.StoreId == storeId)
            .OrderByDescending(c => c.EffectiveFrom).Select(c => (decimal?)c.CommissionPercent).FirstOrDefaultAsync(ct);
        if (storeRule.HasValue) return storeRule.Value;

        if (categoryId.HasValue)
        {
            var catRule = await active.Where(c => c.Scope == CommissionScope.Category && c.CategoryId == categoryId)
                .OrderByDescending(c => c.EffectiveFrom).Select(c => (decimal?)c.CommissionPercent).FirstOrDefaultAsync(ct);
            if (catRule.HasValue) return catRule.Value;
        }

        var globalRule = await active.Where(c => c.Scope == CommissionScope.Global)
            .OrderByDescending(c => c.EffectiveFrom).Select(c => (decimal?)c.CommissionPercent).FirstOrDefaultAsync(ct);
        if (globalRule.HasValue) return globalRule.Value;

        return await _db.Stores.AsNoTracking().Where(s => s.Id == storeId)
            .Select(s => s.CommissionPercent).FirstOrDefaultAsync(ct);
    }
}
