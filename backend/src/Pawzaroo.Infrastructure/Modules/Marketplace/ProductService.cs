using System.Text.Json;
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

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly IMarketplaceCache _cache;

    public ProductService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit, IMarketplaceCache cache)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _cache = cache;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool CanModerateProducts() => _current.Permissions.Contains(Permissions.Products.Feature)
                                       || _current.Permissions.Contains(Permissions.Products.Publish);

    public async Task<PageResult<ProductSummaryDto>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var isHotPath = page == 1 && query.Scope == ProductListingScope.Public;

        string variantKey = isHotPath ? BuildVariantKey(query, pageSize) : "";
        if (isHotPath)
        {
            var cached = await _cache.GetProductFirstPageAsync(variantKey, ct);
            if (cached is not null) return cached;
        }

        var q = _db.Products.AsNoTracking().AsQueryable();

        switch (query.Scope)
        {
            case ProductListingScope.Public:
                q = q.Where(p => p.IsActive && p.Store.ApprovalStatus == ApprovalStatus.Approved);
                break;
            case ProductListingScope.MyStore:
                var uid = Uid();
                var myStoreId = await _db.Stores.AsNoTracking()
                    .Where(s => s.OwnerUserId == uid)
                    .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
                if (myStoreId is null) throw new ForbiddenException("No store registered.");
                q = q.Where(p => p.StoreId == myStoreId);
                break;
            case ProductListingScope.AdminAll:
                if (!CanModerateProducts() && !_current.Permissions.Contains(Permissions.Products.Edit))
                    throw new ForbiddenException();
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(p => p.Name.Contains(query.Search) || p.Sku.Contains(query.Search));
        if (query.CategoryId.HasValue) q = q.Where(p => p.CategoryId == query.CategoryId);
        if (query.BrandId.HasValue)    q = q.Where(p => p.BrandId == query.BrandId);
        if (query.StoreId.HasValue)    q = q.Where(p => p.StoreId == query.StoreId);
        if (query.MinPrice.HasValue)   q = q.Where(p => (p.DiscountPrice ?? p.Price) >= query.MinPrice);
        if (query.MaxPrice.HasValue)   q = q.Where(p => (p.DiscountPrice ?? p.Price) <= query.MaxPrice);
        if (query.Featured == true)    q = q.Where(p => p.IsFeatured);
        if (query.InStockOnly == true) q = q.Where(p => p.StockQuantity > 0);

        q = (query.Sort ?? "newest") switch
        {
            "price_asc"    => q.OrderBy(p => p.DiscountPrice ?? p.Price),
            "price_desc"   => q.OrderByDescending(p => p.DiscountPrice ?? p.Price),
            "rating_desc"  => q.OrderByDescending(p => p.RatingAverage),
            "best_selling" => q.OrderByDescending(p => _db.OrderItems.Count(oi => oi.ProductId == p.Id)),
            _ => q.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt),
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductSummaryDto(
                p.Id, p.Name, p.Sku, p.Price, p.DiscountPrice,
                p.StockQuantity, p.IsActive, p.IsFeatured,
                p.RatingAverage, p.RatingCount,
                p.StoreId, p.Store.Name,
                p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.BrandId, p.Brand != null ? p.Brand.Name : null,
                p.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).ToList(),
                p.CreatedAt))
            .ToListAsync(ct);

        var result = new PageResult<ProductSummaryDto>(items, total, page, pageSize);
        if (isHotPath) await _cache.SetProductFirstPageAsync(variantKey, result, ct);
        return result;
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid productId, CancellationToken ct = default)
    {
        return await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductDetailDto(
                p.Id, p.Name, p.Sku, p.Description, p.Price, p.DiscountPrice,
                p.StockQuantity, p.IsActive, p.IsFeatured,
                p.RatingAverage, p.RatingCount,
                p.StoreId, p.Store.Name, p.Store.ApprovalStatus,
                p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.BrandId, p.Brand != null ? p.Brand.Name : null,
                p.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).ToList(),
                p.Reviews.OrderByDescending(r => r.CreatedAt).Take(5)
                    .Select(r => new ProductReviewDto(
                        r.Id, r.ProductId, r.UserId, r.User.DisplayName, r.User.AvatarUrl,
                        r.Rating, r.Comment, r.CreatedAt,
                        r.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).ToList())).ToList(),
                p.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(CreateProductInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var store = await _db.Stores.FirstOrDefaultAsync(s => s.OwnerUserId == uid, ct)
                    ?? throw new ForbiddenException("Register a store first.");
        if (store.ApprovalStatus != ApprovalStatus.Approved)
            throw new ForbiddenException("Store is not approved.");
        if (await _db.Products.AnyAsync(p => p.StoreId == store.Id && p.Sku == input.Sku, ct))
            throw new ConflictException($"SKU '{input.Sku}' already used by this store.");

        var product = new Product
        {
            StoreId = store.Id,
            Name = input.Name.Trim(),
            Sku = input.Sku.Trim(),
            Description = input.Description,
            Price = input.Price,
            DiscountPrice = input.DiscountPrice,
            StockQuantity = input.StockQuantity,
            CategoryId = input.CategoryId,
            BrandId = input.BrandId,
            IsActive = true
        };
        if (input.ImageUrls is { Count: > 0 })
            for (int i = 0; i < input.ImageUrls.Count; i++)
                product.Images.Add(new ProductImage { Url = input.ImageUrls[i], OrderIndex = i });

        _db.Products.Add(product);

        if (input.StockQuantity > 0)
        {
            _db.InventoryAdjustments.Add(new InventoryAdjustment
            {
                Product = product,
                QuantityChange = input.StockQuantity,
                QuantityAfter = input.StockQuantity,
                Reason = InventoryReason.Restock,
                Notes = "Initial stock on product create",
                PerformedById = uid
            });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.ProductEvents,
            new ProductCreated(product.Id, store.Id, DateTime.UtcNow), product.Id.ToString(), ct);
        await _audit.LogAsync("product.create", "Product", product.Id.ToString(), ct: ct);
        return product.Id;
    }

    public async Task UpdateAsync(Guid productId, UpdateProductInput input, CancellationToken ct = default)
    {
        var product = await _db.Products.Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == productId, ct) ?? throw new NotFoundException("Product", productId);
        await EnsureOwnerOrModerator(product, ct);

        product.Name = input.Name.Trim();
        product.Description = input.Description;
        product.Price = input.Price;
        product.DiscountPrice = input.DiscountPrice;
        product.CategoryId = input.CategoryId;
        product.BrandId = input.BrandId;
        product.IsActive = input.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _current.UserId;

        if (input.ImageUrls is not null)
        {
            _db.ProductImages.RemoveRange(product.Images);
            product.Images.Clear();
            for (int i = 0; i < input.ImageUrls.Count; i++)
                product.Images.Add(new ProductImage { ProductId = product.Id, Url = input.ImageUrls[i], OrderIndex = i });
        }

        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.ProductEvents,
            new ProductUpdated(product.Id, product.StoreId, DateTime.UtcNow), product.Id.ToString(), ct);
        await _audit.LogAsync("product.update", "Product", product.Id.ToString(), ct: ct);
    }

    public async Task DeleteAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        await EnsureOwnerOrModerator(product, ct);

        product.IsDeleted = true;
        product.IsActive = false;
        product.DeletedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.ProductEvents,
            new ProductDeleted(product.Id, product.StoreId, DateTime.UtcNow), product.Id.ToString(), ct);
        await _audit.LogAsync("product.delete", "Product", product.Id.ToString(), ct: ct);
    }

    public async Task SetFeaturedAsync(Guid productId, bool featured, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Products.Feature)) throw new ForbiddenException();
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId, ct) ?? throw new NotFoundException("Product", productId);
        p.IsFeatured = featured;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.ProductEvents,
            new ProductFeatured(productId, featured, DateTime.UtcNow), productId.ToString(), ct);
    }

    public async Task SetActiveAsync(Guid productId, bool active, CancellationToken ct = default)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId, ct) ?? throw new NotFoundException("Product", productId);
        await EnsureOwnerOrModerator(p, ct);
        p.IsActive = active;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.ProductEvents,
            new ProductPublished(productId, active, DateTime.UtcNow), productId.ToString(), ct);
    }

    private async Task EnsureOwnerOrModerator(Product p, CancellationToken ct)
    {
        var uid = _current.UserId;
        if (uid is null) throw new ForbiddenException();
        if (CanModerateProducts() || _current.Permissions.Contains(Permissions.Products.Edit)) return;
        var ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == p.StoreId).Select(s => s.OwnerUserId).FirstOrDefaultAsync(ct);
        if (ownerId != uid) throw new ForbiddenException();
    }

    private static string BuildVariantKey(ProductSearchQuery q, int pageSize) =>
        JsonSerializer.Serialize(new
        {
            q.Search, q.CategoryId, q.BrandId, q.StoreId,
            q.MinPrice, q.MaxPrice, q.Featured, q.InStockOnly, q.Sort, pageSize
        });
}
