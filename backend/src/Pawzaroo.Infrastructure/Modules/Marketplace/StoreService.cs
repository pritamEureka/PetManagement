using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class StoreService : IStoreService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly IMarketplaceCache _cache;

    public StoreService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit, IMarketplaceCache cache)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _cache = cache;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool CanModerate() => _current.Permissions.Contains(Permissions.Stores.Approve)
                               || _current.Permissions.Contains(Permissions.Stores.Suspend);

    public async Task<StoreDto?> GetByIdAsync(Guid storeId, CancellationToken ct = default)
    {
        var cached = await _cache.GetStoreAsync(storeId, ct);
        if (cached is not null) return cached;
        var dto = await ProjectStore(_db.Stores.AsNoTracking().Where(s => s.Id == storeId)).FirstOrDefaultAsync(ct);
        if (dto is not null) await _cache.SetStoreAsync(dto, ct);
        return dto;
    }

    public async Task<StoreDto?> GetMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await ProjectStore(_db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid> RegisterAsync(RegisterStoreInput input, CancellationToken ct = default)
    {
        var uid = Uid();

        // Require approved KYC.
        var kyc = await _db.StoreOwnerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == uid, ct);
        if (kyc is null || kyc.KycStatus != ApprovalStatus.Approved)
            throw new ForbiddenException("KYC must be approved before registering a store.");

        if (await _db.Stores.AnyAsync(s => s.OwnerUserId == uid, ct))
            throw new ConflictException("Store already registered for this user.");

        var store = new Store
        {
            OwnerUserId = uid,
            Name = input.Name.Trim(),
            Description = input.Description,
            LogoUrl = input.LogoUrl,
            BannerUrl = input.BannerUrl,
            Address = input.Address,
            City = input.City,
            Country = input.Country,
            PhoneNumber = input.PhoneNumber,
            Email = input.Email,
            ApprovalStatus = ApprovalStatus.Pending
        };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.StoreEvents,
            new StoreRegistered(store.Id, uid, DateTime.UtcNow), store.Id.ToString(), ct);
        await _audit.LogAsync("store.register", "Store", store.Id.ToString(), ct: ct);
        return store.Id;
    }

    public async Task UpdateAsync(Guid storeId, UpdateStoreInput input, CancellationToken ct = default)
    {
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.Id == storeId, ct) ?? throw new NotFoundException("Store", storeId);
        if (store.OwnerUserId != _current.UserId && !CanModerate())
            throw new ForbiddenException();

        store.Name = input.Name.Trim();
        store.Description = input.Description;
        store.LogoUrl = input.LogoUrl;
        store.BannerUrl = input.BannerUrl;
        store.Address = input.Address;
        store.City = input.City;
        store.Country = input.Country;
        store.PhoneNumber = input.PhoneNumber;
        store.Email = input.Email;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);

        await _cache.InvalidateStoreAsync(storeId, ct);
        await _kafka.PublishAsync(MarketplaceTopics.StoreEvents,
            new StoreUpdated(storeId, DateTime.UtcNow), storeId.ToString(), ct);
    }

    public async Task<PageResult<StoreDto>> SearchAsync(string? search, ApprovalStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Stores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(s => s.Name.Contains(search));
        if (status.HasValue) q = q.Where(s => s.ApprovalStatus == status);

        var total = await q.CountAsync(ct);
        var items = await ProjectStore(q.OrderByDescending(s => s.CreatedAt)
                                        .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PageResult<StoreDto>(items, total, page, pageSize);
    }

    public Task ApproveAsync(Guid storeId, string? notes, CancellationToken ct = default)
        => SetApprovalAsync(storeId, ApprovalStatus.Approved, notes, ct);
    public Task RejectAsync(Guid storeId, string? notes, CancellationToken ct = default)
        => SetApprovalAsync(storeId, ApprovalStatus.Rejected, notes, ct);
    public Task SuspendAsync(Guid storeId, string? notes, CancellationToken ct = default)
        => SetApprovalAsync(storeId, ApprovalStatus.Suspended, notes, ct);
    public Task RestoreAsync(Guid storeId, CancellationToken ct = default)
        => SetApprovalAsync(storeId, ApprovalStatus.Approved, null, ct);

    private async Task SetApprovalAsync(Guid storeId, ApprovalStatus status, string? notes, CancellationToken ct)
    {
        if (!CanModerate()) throw new ForbiddenException();
        var s = await _db.Stores.FirstOrDefaultAsync(x => x.Id == storeId, ct) ?? throw new NotFoundException("Store", storeId);
        s.ApprovalStatus = status;
        if (!string.IsNullOrEmpty(notes)) s.AdminNotes = notes;
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateStoreAsync(storeId, ct);

        var by = _current.UserId ?? Guid.Empty;
        object evt = status switch
        {
            ApprovalStatus.Approved   => new StoreApproved(storeId, by, DateTime.UtcNow),
            ApprovalStatus.Rejected   => new StoreRejected(storeId, by, notes, DateTime.UtcNow),
            ApprovalStatus.Suspended  => new StoreSuspended(storeId, by, notes, DateTime.UtcNow),
            _ => new StoreUpdated(storeId, DateTime.UtcNow)
        };
        await _kafka.PublishAsync(MarketplaceTopics.AdminEvents, evt, storeId.ToString(), ct);
        await _audit.LogAsync($"store.{status.ToString().ToLowerInvariant()}", "Store", storeId.ToString(), notes, ct: ct);
    }

    public async Task SetFeaturedAsync(Guid storeId, bool featured, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Stores.Feature)) throw new ForbiddenException();
        // No explicit IsFeatured column on Store today — store the flag in AdminNotes JSON keyed area
        // or extend the entity. Here we extend behavior by recording an audit event + Kafka.
        await _kafka.PublishAsync(MarketplaceTopics.AdminEvents,
            new StoreFeatured(storeId, featured, DateTime.UtcNow), storeId.ToString(), ct);
        await _audit.LogAsync(featured ? "store.feature" : "store.unfeature", "Store", storeId.ToString(), ct: ct);
    }

    public async Task SetCommissionPercentAsync(Guid storeId, decimal percent, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Stores.Approve)) throw new ForbiddenException();
        if (percent is < 0 or > 100) throw new ValidationException(
            new Dictionary<string, string[]> { ["commissionPercent"] = new[] { "Must be 0..100." } });
        var s = await _db.Stores.FirstOrDefaultAsync(x => x.Id == storeId, ct) ?? throw new NotFoundException("Store", storeId);
        s.CommissionPercent = percent;
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateStoreAsync(storeId, ct);
        await _audit.LogAsync("store.commission_changed", "Store", storeId.ToString(), percent.ToString("0.##"), ct: ct);
    }

    private IQueryable<StoreDto> ProjectStore(IQueryable<Store> q) =>
        q.Select(s => new StoreDto(
            s.Id, s.OwnerUserId, s.Name, s.Description,
            s.LogoUrl, s.BannerUrl,
            s.Address, s.City, s.Country,
            s.PhoneNumber, s.Email,
            s.ApprovalStatus, s.CommissionPercent,
            _db.StoreReviews.Where(r => r.StoreId == s.Id).Select(r => (decimal?)r.Rating).Average() ?? 0m,
            _db.StoreReviews.Count(r => r.StoreId == s.Id),
            s.Products.Count(p => p.IsActive),
            s.CreatedAt));
}
