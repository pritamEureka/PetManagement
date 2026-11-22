using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Modules.Marketplace;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.LoyaltyPlus;

internal static class StoreAccess
{
    /// <summary>Store the user owns (one per owner) or null.</summary>
    public static Task<Guid?> OwnStoreIdAsync(ApplicationDbContext db, Guid uid, CancellationToken ct) =>
        db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);

    public static async Task<bool> OwnsAsync(ApplicationDbContext db, ICurrentUserService c, Guid storeId, CancellationToken ct)
    {
        if (PlusAccess.IsAdmin(c)) return true;
        var uid = c.UserId ?? throw new ForbiddenException();
        return await db.Stores.AnyAsync(s => s.Id == storeId && s.OwnerUserId == uid, ct);
    }
}

// =====================================================================================================
// Bundles
// =====================================================================================================

public class BundleService : IBundleService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICartService _cart;

    public BundleService(ApplicationDbContext db, ICurrentUserService current, ICartService cart)
    { _db = db; _current = current; _cart = cart; }

    public async Task<IReadOnlyList<BundleDto>> ListActiveAsync(int take = 12, CancellationToken ct = default)
    {
        var bundles = await _db.Set<ProductBundle>().AsNoTracking().Include(b => b.Items)
            .Where(b => b.IsActive && _db.Stores.Any(s => s.Id == b.StoreId && s.ApprovalStatus == ApprovalStatus.Approved))
            .OrderByDescending(b => b.CreatedAt).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        return await ToDtosAsync(bundles, ct);
    }

    public async Task<IReadOnlyList<BundleDto>> ListForProductAsync(Guid productId, CancellationToken ct = default)
    {
        var bundles = await _db.Set<ProductBundle>().AsNoTracking().Include(b => b.Items)
            .Where(b => b.IsActive && b.Items.Any(i => i.ProductId == productId)
                        && _db.Stores.Any(s => s.Id == b.StoreId && s.ApprovalStatus == ApprovalStatus.Approved))
            .OrderByDescending(b => b.CreatedAt).Take(10).ToListAsync(ct);
        return await ToDtosAsync(bundles, ct);
    }

    public async Task<IReadOnlyList<BundleDto>> ListForStoreAsync(Guid storeId, bool includeInactive, CancellationToken ct = default)
    {
        if (includeInactive && !await StoreAccess.OwnsAsync(_db, _current, storeId, ct)) throw new ForbiddenException();
        var bundles = await _db.Set<ProductBundle>().AsNoTracking().Include(b => b.Items)
            .Where(b => b.StoreId == storeId && (includeInactive || b.IsActive))
            .OrderByDescending(b => b.CreatedAt).ToListAsync(ct);
        return await ToDtosAsync(bundles, ct);
    }

    public async Task<BundleDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Set<ProductBundle>().AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Bundle", id);
        if (!b.IsActive && !await StoreAccess.OwnsAsync(_db, _current, b.StoreId, ct)) throw new NotFoundException("Bundle", id);
        return (await ToDtosAsync(new[] { b }, ct))[0];
    }

    public async Task<BundleDto> CreateAsync(UpsertBundleInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var storeId = input.StoreId ?? await StoreAccess.OwnStoreIdAsync(_db, uid, ct) ?? throw new ForbiddenException("Register a store first.");
        if (!await StoreAccess.OwnsAsync(_db, _current, storeId, ct)) throw new ForbiddenException();
        await ValidateAsync(input, storeId, ct);

        var b = new ProductBundle { StoreId = storeId, CreatedBy = uid };
        Apply(b, input);
        _db.Set<ProductBundle>().Add(b);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(b.Id, ct);
    }

    public async Task<BundleDto> UpdateAsync(Guid id, UpsertBundleInput input, CancellationToken ct = default)
    {
        var b = await _db.Set<ProductBundle>().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Bundle", id);
        if (!await StoreAccess.OwnsAsync(_db, _current, b.StoreId, ct)) throw new ForbiddenException();
        await ValidateAsync(input, b.StoreId, ct);
        _db.Set<ProductBundleItem>().RemoveRange(b.Items);
        b.Items.Clear();
        Apply(b, input);
        b.UpdatedAt = DateTime.UtcNow; b.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(b.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Set<ProductBundle>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Bundle", id);
        if (!await StoreAccess.OwnsAsync(_db, _current, b.StoreId, ct)) throw new ForbiddenException();
        _db.Set<ProductBundle>().Remove(b);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<CartDto> AddToCartAsync(Guid id, AddBundleToCartInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var qty = Math.Clamp(input.Quantity, 1, 20);
        var bundle = await GetAsync(id, ct);
        if (!bundle.IsActive) throw new ConflictException("Bundle is unavailable.");
        if (bundle.MaxAvailable < qty) throw new ConflictException("Not enough stock for the requested number of bundles.");

        CartDto? cart = null;
        foreach (var i in bundle.Items)
            cart = await _cart.AddAsync(new AddToCartInput(i.ProductId, i.Quantity * qty, i.VariantId), ct);

        var mark = await _db.Set<BundleCartDiscount>().FirstOrDefaultAsync(m => m.UserId == uid && m.BundleId == id, ct);
        if (mark is null) _db.Set<BundleCartDiscount>().Add(new BundleCartDiscount { UserId = uid, BundleId = id, Quantity = qty });
        else mark.Quantity += qty;
        await _db.SaveChangesAsync(ct);
        return cart ?? await _cart.GetMineAsync(ct);
    }

    private async Task ValidateAsync(UpsertBundleInput i, Guid storeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 160) throw PlusAccess.Invalid("name", "Name is required (max 160).");
        if (i.Items is null || i.Items.Count < 2 || i.Items.Count > 10) throw PlusAccess.Invalid("items", "A bundle needs 2-10 items.");
        if (i.Items.Any(x => x.Quantity < 1 || x.Quantity > 50)) throw PlusAccess.Invalid("items", "Quantities must be 1-50.");
        if ((i.Price is null) == (i.DiscountPercent is null)) throw PlusAccess.Invalid("price", "Set either a fixed price or a discount percent.");
        if (i.Price is < 0) throw PlusAccess.Invalid("price", "Price cannot be negative.");
        if (i.DiscountPercent is <= 0 or > 100) throw PlusAccess.Invalid("discountPercent", "Percent must be in (0,100].");

        var pids = i.Items.Select(x => x.ProductId).Distinct().ToList();
        var okCount = await _db.Products.CountAsync(p => pids.Contains(p.Id) && p.StoreId == storeId, ct);
        if (okCount != pids.Count) throw PlusAccess.Invalid("items", "All products must belong to the store.");
        foreach (var v in i.Items.Where(x => x.VariantId != null))
            if (!await _db.Set<ProductVariant>().AnyAsync(x => x.Id == v.VariantId && x.ProductId == v.ProductId, ct))
                throw PlusAccess.Invalid("items", "Variant does not belong to the product.");
    }

    private static void Apply(ProductBundle b, UpsertBundleInput i)
    {
        b.Name = i.Name.Trim(); b.Description = i.Description?.Trim();
        b.Price = i.Price; b.DiscountPercent = i.Price is null ? i.DiscountPercent : null; b.IsActive = i.IsActive;
        foreach (var it in i.Items)
            b.Items.Add(new ProductBundleItem { BundleId = b.Id, ProductId = it.ProductId, VariantId = it.VariantId, Quantity = it.Quantity });
    }

    private async Task<IReadOnlyList<BundleDto>> ToDtosAsync(IReadOnlyList<ProductBundle> bundles, CancellationToken ct)
    {
        if (bundles.Count == 0) return Array.Empty<BundleDto>();
        var pids = bundles.SelectMany(b => b.Items).Select(i => i.ProductId).Distinct().ToList();
        var vids = bundles.SelectMany(b => b.Items).Where(i => i.VariantId != null).Select(i => i.VariantId!.Value).Distinct().ToList();
        var storeIds = bundles.Select(b => b.StoreId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking().Where(p => pids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Price, p.DiscountPrice, p.StockQuantity, p.IsActive,
                Image = p.Images.OrderBy(im => im.OrderIndex).Select(im => im.Url).FirstOrDefault() })
            .ToDictionaryAsync(p => p.Id, ct);
        var variants = await _db.Set<ProductVariant>().AsNoTracking().Where(v => vids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var stores = await _db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return bundles.Select(b =>
        {
            var items = new List<BundleItemDto>();
            var comps = new List<BundlePricing.Component>();
            var stock = new List<(int, int)>();
            var allActive = true;
            foreach (var i in b.Items)
            {
                if (!products.TryGetValue(i.ProductId, out var p)) { allActive = false; continue; }
                ProductVariant? v = null;
                if (i.VariantId is { } vid) variants.TryGetValue(vid, out v);
                var unit = v?.PriceOverride ?? p.DiscountPrice ?? p.Price;
                var st = v?.StockQuantity ?? p.StockQuantity;
                if (!p.IsActive || (i.VariantId != null && (v is null || !v.IsActive))) allActive = false;
                items.Add(new BundleItemDto(p.Id, p.Name, p.Image, i.VariantId, v?.Sku, i.Quantity, unit, st));
                comps.Add(new BundlePricing.Component(p.Id, i.VariantId, i.Quantity, unit));
                stock.Add((i.Quantity, st));
            }
            var list = BundlePricing.ListPrice(comps);
            var price = BundlePricing.BundlePrice(list, b.Price, b.DiscountPercent);
            return new BundleDto(b.Id, b.StoreId, stores.GetValueOrDefault(b.StoreId) ?? "", b.Name, b.Description,
                b.Price, b.DiscountPercent, b.IsActive && allActive, list, price, list - price,
                allActive ? BundlePricing.MaxBundlesByStock(stock) : 0, items);
        }).ToList();
    }
}

// =====================================================================================================
// Low-stock alerts + replenishment
// =====================================================================================================

public class StockAlertService : IStockAlertService
{
    public static readonly TimeSpan ReAlertAfter = TimeSpan.FromDays(7);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;
    private readonly LoyaltyPlusOptions _opt;

    public StockAlertService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify, LoyaltyPlusOptions opt)
    { _db = db; _current = current; _notify = notify; _opt = opt; }

    public async Task<int> RunLowStockAlertsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var def = _opt.LowStockDefaultThreshold;
        var thresholds = await _db.Set<ProductStockThreshold>().ToListAsync(ct);
        var explicitIds = thresholds.Select(t => t.ProductId).Distinct().ToList();

        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Store.ApprovalStatus == ApprovalStatus.Approved
                        && (p.StockQuantity <= def || explicitIds.Contains(p.Id)))
            .Select(p => new { p.Id, p.Name, p.StoreId, p.StockQuantity, OwnerId = p.Store.OwnerUserId }).Take(2000).ToListAsync(ct);
        var variantRows = await _db.Set<ProductVariant>().AsNoTracking()
            .Where(v => v.IsActive && v.Product.IsActive && (v.StockQuantity <= def || explicitIds.Contains(v.ProductId)))
            .Select(v => new { v.Id, v.ProductId, v.Sku, v.StockQuantity, v.Product.Name, v.Product.StoreId, OwnerId = v.Product.Store.OwnerUserId })
            .Take(2000).ToListAsync(ct);

        var pending = new Dictionary<Guid, List<string>>(); // owner -> lines
        void Check(Guid productId, Guid? variantId, int stock, string label, Guid owner)
        {
            var row = thresholds.FirstOrDefault(t => t.ProductId == productId && t.VariantId == variantId);
            var th = row?.Threshold ?? def;
            if (stock > th)
            {
                if (row is { LastAlertedAt: not null }) row.LastAlertedAt = null; // recovered: re-arm
                return;
            }
            if (row?.LastAlertedAt is { } last && now - last < ReAlertAfter) return; // de-dup
            if (row is null)
            {
                row = new ProductStockThreshold { ProductId = productId, VariantId = variantId, Threshold = th };
                _db.Set<ProductStockThreshold>().Add(row);
                thresholds.Add(row);
            }
            row.LastAlertedAt = now;
            if (!pending.TryGetValue(owner, out var l)) pending[owner] = l = new List<string>();
            l.Add($"{label}: {stock} left");
        }

        foreach (var p in products) Check(p.Id, null, p.StockQuantity, p.Name, p.OwnerId);
        foreach (var v in variantRows) Check(v.ProductId, v.Id, v.StockQuantity, $"{v.Name} ({v.Sku})", v.OwnerId);
        await _db.SaveChangesAsync(ct);

        foreach (var (owner, lines) in pending)
            await _notify.NotifyUserAsync(owner, "Low stock alert",
                string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : ""),
                new { lowStockCount = lines.Count }, ct);
        return pending.Sum(p => p.Value.Count);
    }

    public async Task<IReadOnlyList<StockThresholdDto>> ListForStoreAsync(Guid storeId, CancellationToken ct = default)
    {
        if (!await StoreAccess.OwnsAsync(_db, _current, storeId, ct)) throw new ForbiddenException();
        var def = _opt.LowStockDefaultThreshold;
        var products = await _db.Products.AsNoTracking().Where(p => p.StoreId == storeId)
            .OrderBy(p => p.StockQuantity).Take(200).Select(p => new { p.Id, p.Name, p.StockQuantity }).ToListAsync(ct);
        var ids = products.Select(p => p.Id).ToList();
        var th = await _db.Set<ProductStockThreshold>().AsNoTracking().Where(t => ids.Contains(t.ProductId) && t.VariantId == null).ToListAsync(ct);
        return products.Select(p =>
        {
            var t = th.FirstOrDefault(x => x.ProductId == p.Id);
            var thr = t?.Threshold ?? def;
            return new StockThresholdDto(p.Id, null, p.Name, null, p.StockQuantity, thr, t?.LastAlertedAt, p.StockQuantity <= thr);
        }).ToList();
    }

    public async Task SetThresholdAsync(SetStockThresholdInput input, CancellationToken ct = default)
    {
        if (input.Threshold < 0 || input.Threshold > 100000) throw PlusAccess.Invalid("threshold", "Threshold must be 0-100000.");
        var storeId = await _db.Products.AsNoTracking().Where(p => p.Id == input.ProductId).Select(p => (Guid?)p.StoreId).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Product", input.ProductId);
        if (!await StoreAccess.OwnsAsync(_db, _current, storeId, ct)) throw new ForbiddenException();
        var row = await _db.Set<ProductStockThreshold>().FirstOrDefaultAsync(t => t.ProductId == input.ProductId && t.VariantId == input.VariantId, ct);
        if (row is null) _db.Set<ProductStockThreshold>().Add(new ProductStockThreshold { ProductId = input.ProductId, VariantId = input.VariantId, Threshold = input.Threshold });
        else { row.Threshold = input.Threshold; row.LastAlertedAt = null; row.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }
}

public class ReplenishmentService : IReplenishmentService
{
    private static readonly TimeSpan LeadTime = TimeSpan.FromDays(3);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;

    public ReplenishmentService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify)
    { _db = db; _current = current; _notify = notify; }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-365);
        // Repeat purchases of the same product on delivered orders = consumable heuristic.
        var rows = await _db.Set<OrderItem>().AsNoTracking()
            .Where(i => i.Order.Status == OrderStatus.Delivered && i.Order.CreatedAt >= since)
            .Select(i => new { i.Order.UserId, i.ProductId, At = i.Order.CreatedAt, i.Quantity })
            .Take(20000).ToListAsync(ct);

        var existing = await _db.Set<ReplenishmentReminder>().ToListAsync(ct);
        foreach (var g in rows.GroupBy(r => (r.UserId, r.ProductId)))
        {
            var purchases = g.Select(x => new ReplenishmentEstimator.Purchase(x.At, x.Quantity)).ToList();
            if (purchases.Count < 2) continue;
            var runOut = ReplenishmentEstimator.EstimateRunOut(purchases);
            if (runOut is null) continue;
            var r = existing.FirstOrDefault(e => e.UserId == g.Key.UserId && e.ProductId == g.Key.ProductId);
            if (r is null)
            {
                r = new ReplenishmentReminder { UserId = g.Key.UserId, ProductId = g.Key.ProductId, ExpectedRunOutAt = runOut.Value };
                _db.Set<ReplenishmentReminder>().Add(r);
                existing.Add(r);
            }
            else if (runOut.Value > r.ExpectedRunOutAt.AddDays(3)) // bought again: re-arm
            {
                r.ExpectedRunOutAt = runOut.Value; r.RemindedAt = null; r.Dismissed = false; r.UpdatedAt = now;
            }
        }
        await _db.SaveChangesAsync(ct);

        var due = await _db.Set<ReplenishmentReminder>()
            .Where(r => !r.Dismissed && r.RemindedAt == null && r.ExpectedRunOutAt <= now + LeadTime)
            .Take(500).ToListAsync(ct);
        var sent = 0;
        foreach (var r in due)
        {
            var p = await _db.Products.AsNoTracking().Where(x => x.Id == r.ProductId && x.IsActive && x.StockQuantity > 0)
                .Select(x => new { x.Name }).FirstOrDefaultAsync(ct);
            if (p is null) continue;
            r.RemindedAt = now;
            await _notify.NotifyUserAsync(r.UserId, "Time to restock?", $"You may be running low on {p.Name}. Reorder in one tap.",
                new { productId = r.ProductId, kind = "replenishment" }, ct);
            sent++;
        }
        await _db.SaveChangesAsync(ct);
        return sent;
    }

    public async Task<IReadOnlyList<ReplenishmentReminderDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var horizon = DateTime.UtcNow.AddDays(7);
        var now = DateTime.UtcNow;
        var rows = await _db.Set<ReplenishmentReminder>().AsNoTracking()
            .Where(r => r.UserId == uid && !r.Dismissed && r.ExpectedRunOutAt <= horizon)
            .OrderBy(r => r.ExpectedRunOutAt).Take(10).ToListAsync(ct);
        var ids = rows.Select(r => r.ProductId).ToList();
        var products = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Price, p.DiscountPrice, p.StockQuantity,
                Image = p.Images.OrderBy(im => im.OrderIndex).Select(im => im.Url).FirstOrDefault() })
            .ToDictionaryAsync(p => p.Id, ct);
        return rows.Where(r => products.ContainsKey(r.ProductId)).Select(r =>
        {
            var p = products[r.ProductId];
            return new ReplenishmentReminderDto(r.Id, r.ProductId, p.Name, p.Image, p.DiscountPrice ?? p.Price,
                p.StockQuantity > 0, r.ExpectedRunOutAt, (int)Math.Ceiling((r.ExpectedRunOutAt - now).TotalDays));
        }).ToList();
    }

    public async Task DismissAsync(Guid id, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.Set<ReplenishmentReminder>().Where(r => r.Id == id && r.UserId == uid)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Dismissed, true), ct);
        if (rows == 0) throw new NotFoundException("ReplenishmentReminder", id);
    }
}

// =====================================================================================================
// Store comparison, delivery promise, availability
// =====================================================================================================

public class StoreComparisonService : IStoreComparisonService
{
    private readonly ApplicationDbContext _db;
    private readonly MarketplaceOptions _market;

    public StoreComparisonService(ApplicationDbContext db, IOptions<MarketplaceOptions> market) { _db = db; _market = market.Value; }

    public async Task<StoreComparisonDto> CompareAsync(IReadOnlyList<Guid> storeIds, CancellationToken ct = default)
    {
        var ids = (storeIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count < 2 || ids.Count > 4) throw PlusAccess.Invalid("storeIds", "Select between 2 and 4 stores.");

        var stores = await _db.Stores.AsNoTracking()
            .Where(s => ids.Contains(s.Id) && s.ApprovalStatus == ApprovalStatus.Approved).ToListAsync(ct);
        if (stores.Count != ids.Count) throw new NotFoundException("Store", string.Join(",", ids));

        var reviews = await _db.StoreReviews.AsNoTracking().Where(r => ids.Contains(r.StoreId))
            .GroupBy(r => r.StoreId).Select(g => new { StoreId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.StoreId, ct);

        var since = DateTime.UtcNow.AddDays(-180);
        var orders = await _db.Set<OrderItem>().AsNoTracking()
            .Where(i => ids.Contains(i.StoreId) && i.Order.CreatedAt >= since
                        && (i.Order.Status == OrderStatus.Delivered || i.Order.Status == OrderStatus.Returned))
            .Select(i => new { i.StoreId, i.OrderId, i.Order.Status, i.Order.CreatedAt, i.Order.UpdatedAt,
                Delivered = i.Order.DeliveryAssignment != null ? i.Order.DeliveryAssignment.DeliveredAt : null })
            .Distinct().ToListAsync(ct);

        var products = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.StoreId) && p.IsActive)
            .Select(p => new { p.StoreId, p.Name, Price = p.DiscountPrice ?? p.Price }).ToListAsync(ct);
        var byName = products.GroupBy(p => p.Name.Trim().ToLowerInvariant())
            .Where(g => g.Select(x => x.StoreId).Distinct().Count() >= 2).ToList();
        var ratios = new Dictionary<Guid, List<decimal>>();
        foreach (var g in byName)
        {
            var prices = g.Select(x => x.Price).OrderBy(x => x).ToList();
            var median = prices[prices.Count / 2];
            if (median <= 0) continue;
            foreach (var sg in g.GroupBy(x => x.StoreId))
            {
                if (!ratios.TryGetValue(sg.Key, out var l)) ratios[sg.Key] = l = new List<decimal>();
                l.Add(sg.Average(x => x.Price) / median);
            }
        }

        var items = ids.Select(id =>
        {
            var s = stores.First(x => x.Id == id);
            var os = orders.Where(o => o.StoreId == id).ToList();
            var deliveredOs = os.Where(o => o.Status == OrderStatus.Delivered).ToList();
            double? avg = deliveredOs.Count == 0 ? null
                : deliveredOs.Average(o => ((o.Delivered ?? o.UpdatedAt ?? o.CreatedAt) - o.CreatedAt).TotalDays);
            double? ret = os.Count == 0 ? null : 100.0 * os.Count(o => o.Status == OrderStatus.Returned) / os.Count;
            reviews.TryGetValue(id, out var rv);
            ratios.TryGetValue(id, out var rl);
            return new StoreComparisonItemDto(id, s.Name, s.City, s.LogoUrl,
                rv is null ? null : Math.Round((decimal)rv.Avg, 2), rv?.Count ?? 0,
                avg is null ? null : Math.Round(avg.Value, 1), deliveredOs.Count,
                ret is null ? null : Math.Round(ret.Value, 1),
                rl is { Count: > 0 } ? Math.Round(rl.Average() * 100m, 1) : null, rl?.Count ?? 0,
                _market.ShippingFlatFee, _market.FreeShippingThreshold,
                products.Count(p => p.StoreId == id));
        }).ToList();
        return new StoreComparisonDto(items);
    }
}

public class DeliveryPromiseService : IDeliveryPromiseService
{
    public const double DefaultAverageDays = 3.0;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEnumerable<IDeliveryEtaContributor> _contributors;

    public DeliveryPromiseService(ApplicationDbContext db, ICurrentUserService current,
        IEnumerable<IDeliveryEtaContributor> contributors)
    { _db = db; _current = current; _contributors = contributors; }

    public async Task<DeliveryPromiseDto> EstimateAsync(Guid storeId, string? destinationCity, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-90);
        var samples = await _db.Set<OrderItem>().AsNoTracking()
            .Where(i => i.StoreId == storeId && i.Order.Status == OrderStatus.Delivered && i.Order.CreatedAt >= since)
            .Select(i => new { i.OrderId, i.Order.CreatedAt, i.Order.UpdatedAt,
                Delivered = i.Order.DeliveryAssignment != null ? i.Order.DeliveryAssignment.DeliveredAt : null })
            .Distinct().Take(500).ToListAsync(ct);

        var notes = new List<string>();
        double avg; string basis;
        if (samples.Count >= 3)
        {
            avg = samples.Average(s => ((s.Delivered ?? s.UpdatedAt ?? s.CreatedAt) - s.CreatedAt).TotalDays);
            basis = "history";
            notes.Add($"Based on {samples.Count} recent deliveries from this store.");
        }
        else { avg = DefaultAverageDays; basis = "default"; notes.Add("Not enough delivery history yet; using a standard estimate."); }

        double extraMin = 0, extraMax = 0;
        foreach (var c in _contributors)
        {
            var x = await c.ContributeAsync(storeId, destinationCity, ct);
            if (x is null) continue;
            extraMin += x.ExtraMinDays; extraMax += x.ExtraMaxDays;
            if (!string.IsNullOrWhiteSpace(x.Note)) notes.Add(x.Note!);
        }
        // With no delivery history the 3-day default is only a stand-in; once logistics knows the zone it replaces it
        // (otherwise zone hours would be stacked on a generic guess). History already includes processing, so it stays additive.
        if (basis == "default" && (extraMin > 0 || extraMax > 0)) avg = 0;
        var (min, max) = DeliveryEtaMath.Range(avg, extraMin, extraMax);
        var today = DateTime.UtcNow.Date;
        return new DeliveryPromiseDto(storeId, min, max, today.AddDays(min), today.AddDays(max), basis, samples.Count, notes);
    }

    public async Task<CartDeliveryPromiseDto?> EstimateForCartAsync(string? destinationCity, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var storeIds = await _db.CartItems.AsNoTracking().Where(c => c.UserId == uid)
            .Select(c => c.Product.StoreId).Distinct().ToListAsync(ct);
        if (storeIds.Count == 0) return null;
        var list = new List<DeliveryPromiseDto>();
        foreach (var s in storeIds) list.Add(await EstimateAsync(s, destinationCity, ct));
        return new CartDeliveryPromiseDto(list.Min(x => x.EarliestDate), list.Max(x => x.LatestDate), list);
    }
}

public class CartAvailabilityService : ICartAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public CartAvailabilityService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    public async Task<CartAvailabilityDto> CheckAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var lines = await _db.CartItems.AsNoTracking().Where(c => c.UserId == uid)
            .Select(c => new { c.Id, c.ProductId, c.Quantity, c.Product.Name, c.Product.IsActive, c.Product.StockQuantity,
                Store = c.Product.Store.ApprovalStatus }).ToListAsync(ct);
        var ids = lines.Select(l => l.Id).ToList();
        var links = await _db.Set<CartItemVariant>().AsNoTracking().Where(l => ids.Contains(l.CartItemId))
            .ToDictionaryAsync(l => l.CartItemId, l => l.VariantId, ct);
        var vids = links.Values.Distinct().ToList();
        var variants = await _db.Set<ProductVariant>().AsNoTracking().Where(v => vids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

        var problems = new List<AvailabilityProblemDto>();
        foreach (var l in lines)
        {
            if (!l.IsActive) { problems.Add(new(l.Id, l.ProductId, l.Name, "product_inactive", "This product is no longer available.", 0)); continue; }
            if (l.Store != ApprovalStatus.Approved) { problems.Add(new(l.Id, l.ProductId, l.Name, "store_unavailable", "The store is currently not accepting orders.", 0)); continue; }
            var stock = l.StockQuantity;
            if (links.TryGetValue(l.Id, out var vid))
            {
                if (!variants.TryGetValue(vid, out var v) || !v.IsActive)
                { problems.Add(new(l.Id, l.ProductId, l.Name, "variant_inactive", "The selected option is no longer available.", 0)); continue; }
                stock = v.StockQuantity;
            }
            if (stock <= 0) problems.Add(new(l.Id, l.ProductId, l.Name, "out_of_stock", "Out of stock.", 0));
            else if (stock < l.Quantity) problems.Add(new(l.Id, l.ProductId, l.Name, "insufficient_stock", $"Only {stock} left.", stock));
        }
        return new CartAvailabilityDto(problems.Count == 0, problems);
    }
}

// =====================================================================================================
// Sponsored placements
// =====================================================================================================

public class SponsoredPlacementService : ISponsoredPlacementService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public SponsoredPlacementService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    public async Task<IReadOnlyList<SponsoredPlacementDto>> ListAsync(Guid? storeId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var q = _db.Set<SponsoredPlacement>().AsNoTracking().AsQueryable();
        if (PlusAccess.IsAdmin(_current)) { if (storeId is { } s) q = q.Where(p => p.StoreId == s); }
        else
        {
            var own = await StoreAccess.OwnStoreIdAsync(_db, uid, ct) ?? throw new ForbiddenException();
            q = q.Where(p => p.StoreId == own);
        }
        var rows = await q.OrderByDescending(p => p.CreatedAt).Take(300).ToListAsync(ct);
        var pids = rows.Select(r => r.ProductId).ToList(); var sids = rows.Select(r => r.StoreId).ToList();
        var pn = await _db.Products.AsNoTracking().Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var sn = await _db.Stores.AsNoTracking().Where(s => sids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return rows.Select(r => new SponsoredPlacementDto(r.Id, r.ProductId, pn.GetValueOrDefault(r.ProductId) ?? "", r.StoreId,
            sn.GetValueOrDefault(r.StoreId) ?? "", r.StartsAt, r.EndsAt, r.Budget, r.Spent, r.BidWeight, r.IsActive,
            r.Status, r.Impressions, r.CreatedAt)).ToList();
    }

    public async Task<SponsoredPlacementDto> CreateAsync(UpsertSponsoredPlacementInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        Validate(input);
        var product = await _db.Products.AsNoTracking().Where(p => p.Id == input.ProductId)
            .Select(p => new { p.StoreId, p.Store.OwnerUserId }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product", input.ProductId);
        var admin = PlusAccess.IsAdmin(_current);
        if (!admin && product.OwnerUserId != uid) throw new ForbiddenException();

        var p2 = new SponsoredPlacement
        {
            ProductId = input.ProductId, StoreId = product.StoreId, StartsAt = input.StartsAt, EndsAt = input.EndsAt,
            Budget = input.Budget, BidWeight = admin ? Math.Clamp(input.BidWeight ?? 1m, 0.1m, 10m) : 1m,
            IsActive = input.IsActive ?? true, RequestedByUserId = uid, CreatedBy = uid,
            Status = admin ? SponsoredPlacementStatus.Approved : SponsoredPlacementStatus.Pending
        };
        _db.Set<SponsoredPlacement>().Add(p2);
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(admin ? p2.StoreId : null, ct)).First(x => x.Id == p2.Id);
    }

    public async Task<SponsoredPlacementDto> UpdateAsync(Guid id, UpsertSponsoredPlacementInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        Validate(input);
        var row = await _db.Set<SponsoredPlacement>().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("SponsoredPlacement", id);
        var admin = PlusAccess.IsAdmin(_current);
        if (!admin)
        {
            if (!await StoreAccess.OwnsAsync(_db, _current, row.StoreId, ct)) throw new ForbiddenException();
            if (row.Status != SponsoredPlacementStatus.Pending) throw new ConflictException("Only pending requests can be edited.");
        }
        row.StartsAt = input.StartsAt; row.EndsAt = input.EndsAt; row.Budget = input.Budget;
        if (admin) { row.BidWeight = Math.Clamp(input.BidWeight ?? row.BidWeight, 0.1m, 10m); }
        if (input.IsActive is { } a) row.IsActive = a;
        row.UpdatedAt = DateTime.UtcNow; row.UpdatedBy = uid;
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(row.StoreId, ct)).First(x => x.Id == id);
    }

    public async Task ReviewAsync(Guid id, bool approve, CancellationToken ct = default)
    {
        if (!PlusAccess.IsAdmin(_current)) throw new ForbiddenException();
        var status = approve ? SponsoredPlacementStatus.Approved : SponsoredPlacementStatus.Rejected;
        var rows = await _db.Set<SponsoredPlacement>().Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status), ct);
        if (rows == 0) throw new NotFoundException("SponsoredPlacement", id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.Set<SponsoredPlacement>().FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("SponsoredPlacement", id);
        if (!await StoreAccess.OwnsAsync(_db, _current, row.StoreId, ct)) throw new ForbiddenException();
        _db.Set<SponsoredPlacement>().Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(UpsertSponsoredPlacementInput i)
    {
        if (i.EndsAt <= i.StartsAt) throw PlusAccess.Invalid("endsAt", "End must be after start.");
        if (i.Budget is < 0) throw PlusAccess.Invalid("budget", "Budget cannot be negative.");
    }
}
