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

public class ProductCategoryService : IProductCategoryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ProductCategoryService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await _db.ProductCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.Slug, c.ParentCategoryId, c.Products.Count))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<Guid> CreateAsync(CreateProductCategoryInput input, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.Edit)) throw new ForbiddenException();
        if (await _db.ProductCategories.AnyAsync(c => c.Slug == input.Slug, ct))
            throw new ConflictException($"Category slug '{input.Slug}' already exists.");
        var cat = new ProductCategory { Name = input.Name, Slug = input.Slug, ParentCategoryId = input.ParentCategoryId };
        _db.ProductCategories.Add(cat);
        await _db.SaveChangesAsync(ct);
        return cat.Id;
    }

    public async Task UpdateAsync(Guid id, CreateProductCategoryInput input, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.Edit)) throw new ForbiddenException();
        var c = await _db.ProductCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ProductCategory", id);
        c.Name = input.Name; c.Slug = input.Slug; c.ParentCategoryId = input.ParentCategoryId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.Edit)) throw new ForbiddenException();
        var c = await _db.ProductCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ProductCategory", id);
        c.IsDeleted = true; c.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class ProductReviewService : IProductReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public ProductReviewService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<PageResult<ProductReviewDto>> ListAsync(Guid productId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ProductReviews.AsNoTracking().Where(r => r.ProductId == productId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ProductReviewDto(
                r.Id, r.ProductId, r.UserId, r.User.DisplayName, r.User.AvatarUrl,
                r.Rating, r.Comment, r.CreatedAt)).ToListAsync(ct);
        return new PageResult<ProductReviewDto>(items, total, page, pageSize);
    }

    public async Task<ProductReviewDto> CreateAsync(Guid productId, CreateProductReviewInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();

        var hasPurchased = await _db.OrderItems.AsNoTracking()
            .AnyAsync(i => i.ProductId == productId && i.Order.UserId == uid && i.Order.Status == OrderStatus.Delivered, ct);
        if (!hasPurchased) throw new ForbiddenException("You can only review products you have purchased.");

        if (await _db.ProductReviews.AnyAsync(r => r.ProductId == productId && r.UserId == uid, ct))
            throw new ConflictException("You have already reviewed this product.");

        var review = new ProductReview { ProductId = productId, UserId = uid, Rating = input.Rating, Comment = input.Comment };
        _db.ProductReviews.Add(review);

        var stats = await _db.ProductReviews.AsNoTracking().Where(r => r.ProductId == productId)
            .GroupBy(r => 1).Select(g => new { Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .FirstOrDefaultAsync(ct);
        var avg = stats is null ? input.Rating : ((stats.Avg * stats.Count) + input.Rating) / (stats.Count + 1);
        var count = (stats?.Count ?? 0) + 1;

        await _db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.RatingAverage, (decimal)avg)
            .SetProperty(p => p.RatingCount, count), ct);

        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.ReviewEvents,
            new ProductReviewCreated(review.Id, productId, uid, review.Rating, DateTime.UtcNow), productId.ToString(), ct);

        var user = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => new { u.DisplayName, u.AvatarUrl }).FirstAsync(ct);
        return new ProductReviewDto(review.Id, productId, uid, user.DisplayName, user.AvatarUrl, review.Rating, review.Comment, review.CreatedAt);
    }

    public async Task DeleteAsync(Guid reviewId, CancellationToken ct = default)
    {
        var r = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == reviewId, ct) ?? throw new NotFoundException("ProductReview", reviewId);
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (r.UserId != uid && !_current.Permissions.Contains(Permissions.Reviews.Delete)) throw new ForbiddenException();
        _db.ProductReviews.Remove(r);
        await _db.SaveChangesAsync(ct);
    }
}

public class StoreReviewService : IStoreReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public StoreReviewService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task<PageResult<StoreReviewDto>> ListAsync(Guid storeId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.StoreReviews.AsNoTracking().Where(r => r.StoreId == storeId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new StoreReviewDto(
                r.Id, r.StoreId, r.UserId, r.User.DisplayName, r.User.AvatarUrl,
                r.Rating, r.Comment, r.IsVerifiedPurchase, r.CreatedAt)).ToListAsync(ct);
        return new PageResult<StoreReviewDto>(items, total, page, pageSize);
    }

    public async Task<StoreReviewDto> CreateAsync(Guid storeId, CreateStoreReviewInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        bool verified = false;
        if (input.OrderId.HasValue)
        {
            verified = await _db.OrderItems.AsNoTracking()
                .AnyAsync(i => i.OrderId == input.OrderId && i.StoreId == storeId && i.Order.UserId == uid && i.Order.Status == OrderStatus.Delivered, ct);
            if (!verified) throw new ForbiddenException("Order does not contain items from this store or is not delivered.");
        }

        if (await _db.StoreReviews.AnyAsync(r => r.StoreId == storeId && r.UserId == uid && r.OrderId == input.OrderId, ct))
            throw new ConflictException("Review already exists for this order/store.");

        var rv = new StoreReview
        {
            StoreId = storeId, UserId = uid, OrderId = input.OrderId,
            Rating = input.Rating, Comment = input.Comment, IsVerifiedPurchase = verified
        };
        _db.StoreReviews.Add(rv);
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.ReviewEvents,
            new StoreReviewCreated(rv.Id, storeId, uid, rv.Rating, DateTime.UtcNow), storeId.ToString(), ct);

        var user = await _db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => new { u.DisplayName, u.AvatarUrl }).FirstAsync(ct);
        return new StoreReviewDto(rv.Id, storeId, uid, user.DisplayName, user.AvatarUrl,
            rv.Rating, rv.Comment, rv.IsVerifiedPurchase, rv.CreatedAt);
    }
}

public class ShippingAddressService : IShippingAddressService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    public ShippingAddressService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<ShippingAddressDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.ShippingAddresses.AsNoTracking().Where(a => a.UserId == uid)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.CreatedAt)
            .Select(a => new ShippingAddressDto(a.Id, a.Label, a.RecipientName, a.PhoneNumber,
                a.AddressLine1, a.AddressLine2, a.City, a.State, a.Country, a.PostalCode, a.IsDefault))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(UpsertShippingAddressInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (input.IsDefault) await ClearDefaults(uid, ct);
        var a = Map(new ShippingAddress { UserId = uid }, input);
        _db.ShippingAddresses.Add(a);
        await _db.SaveChangesAsync(ct);
        return a.Id;
    }

    public async Task UpdateAsync(Guid id, UpsertShippingAddressInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var a = await _db.ShippingAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("ShippingAddress", id);
        if (input.IsDefault && !a.IsDefault) await ClearDefaults(uid, ct);
        Map(a, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var a = await _db.ShippingAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("ShippingAddress", id);
        a.IsDeleted = true; a.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetDefaultAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var a = await _db.ShippingAddresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("ShippingAddress", id);
        await ClearDefaults(uid, ct);
        a.IsDefault = true;
        await _db.SaveChangesAsync(ct);
    }

    private Task ClearDefaults(Guid uid, CancellationToken ct) =>
        _db.ShippingAddresses.Where(a => a.UserId == uid && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    private static ShippingAddress Map(ShippingAddress a, UpsertShippingAddressInput i)
    {
        a.Label = i.Label; a.RecipientName = i.RecipientName; a.PhoneNumber = i.PhoneNumber;
        a.AddressLine1 = i.AddressLine1; a.AddressLine2 = i.AddressLine2;
        a.City = i.City; a.State = i.State; a.Country = i.Country; a.PostalCode = i.PostalCode;
        a.IsDefault = i.IsDefault;
        a.UpdatedAt = DateTime.UtcNow;
        return a;
    }
}

public class ReturnService : IReturnService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public ReturnService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db; _current = current; _kafka = kafka; _audit = audit;
    }

    public async Task<ReturnRequestDto> CreateAsync(CreateReturnRequestInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var oi = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Id == input.OrderItemId)
            .Select(i => new { i.Id, i.Order.UserId, i.Order.Status })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("OrderItem", input.OrderItemId);
        if (oi.UserId != uid) throw new ForbiddenException();
        if (oi.Status != OrderStatus.Delivered) throw new ConflictException("Only delivered orders can be returned.");

        var r = new ReturnRequest { OrderItemId = input.OrderItemId, UserId = uid, Reason = input.Reason };
        _db.ReturnRequests.Add(r);
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new ReturnRequested(r.Id, r.OrderItemId, uid, DateTime.UtcNow), r.Id.ToString(), ct);
        return new ReturnRequestDto(r.Id, r.OrderItemId, r.UserId, r.Reason, r.Status, r.RefundAmount, r.CreatedAt);
    }

    public Task ApproveAsync(Guid requestId, decimal? refundAmount, CancellationToken ct = default) =>
        DecideAsync(requestId, ApprovalStatus.Approved, refundAmount, null, ct);

    public Task RejectAsync(Guid requestId, string? notes, CancellationToken ct = default) =>
        DecideAsync(requestId, ApprovalStatus.Rejected, null, notes, ct);

    private async Task DecideAsync(Guid id, ApprovalStatus status, decimal? amount, string? notes, CancellationToken ct)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.Refund)) throw new ForbiddenException();
        var r = await _db.ReturnRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ReturnRequest", id);
        r.Status = status;
        r.RefundAmount = amount;
        r.UpdatedAt = DateTime.UtcNow;
        r.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new ReturnDecided(r.Id, r.OrderItemId, status.ToString(), amount, DateTime.UtcNow), r.Id.ToString(), ct);
        await _audit.LogAsync($"return.{status.ToString().ToLowerInvariant()}", "ReturnRequest", r.Id, notes ?? amount?.ToString("0.00"), ct);
    }

    public async Task<PageResult<ReturnRequestDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.View)) throw new ForbiddenException();
        var q = _db.ReturnRequests.AsNoTracking();
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReturnRequestDto(r.Id, r.OrderItemId, r.UserId, r.Reason, r.Status, r.RefundAmount, r.CreatedAt))
            .ToListAsync(ct);
        return new PageResult<ReturnRequestDto>(items, total, page, pageSize);
    }
}

public class SalesReportService : ISalesReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public SalesReportService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<StoreSalesReportDto> ForStoreAsync(Guid storeId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var ownsStore = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == storeId && s.OwnerUserId == uid, ct);
        if (!ownsStore && !_current.Permissions.Contains(Permissions.Reports.View)) throw new ForbiddenException();

        var lines = _db.OrderItems.AsNoTracking()
            .Where(i => i.StoreId == storeId && i.Order.CreatedAt >= from && i.Order.CreatedAt < to
                        && i.Order.Status != OrderStatus.Cancelled);

        var gross = await lines.SumAsync(i => (decimal?)i.Total, ct) ?? 0m;
        var commission = await lines.SumAsync(i => (decimal?)i.CommissionAmount, ct) ?? 0m;
        var orders = await lines.Select(i => i.OrderId).Distinct().CountAsync(ct);
        var units = await lines.SumAsync(i => (int?)i.Quantity, ct) ?? 0;

        var daily = await lines
            .GroupBy(i => i.Order.CreatedAt.Date)
            .Select(g => new DailySalesPoint(DateOnly.FromDateTime(g.Key), g.Sum(x => x.Total), g.Select(x => x.OrderId).Distinct().Count()))
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

        var top = await lines.GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new TopProductDto(g.Key.ProductId, g.Key.Name, g.Sum(x => x.Quantity), g.Sum(x => x.Total)))
            .OrderByDescending(t => t.Revenue).Take(5).ToListAsync(ct);

        return new StoreSalesReportDto(storeId, from, to, gross, commission, gross - commission,
            orders, units, daily, top);
    }
}
