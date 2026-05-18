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

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly IMarketplaceCache _cache;

    private const int LowStockThreshold = 5;

    public InventoryService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit, IMarketplaceCache cache)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _cache = cache;
    }

    public async Task<InventoryAdjustmentDto> AdjustAsync(Guid productId, AdjustInventoryInput input, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        await EnsureOwnerOrAdmin(product, ct);

        var newQty = product.StockQuantity + input.QuantityChange;
        if (newQty < 0) throw new ValidationException(
            new Dictionary<string, string[]> { ["quantityChange"] = new[] { "Adjustment would drive stock below zero." } });

        product.StockQuantity = newQty;
        var adj = new InventoryAdjustment
        {
            ProductId = product.Id,
            QuantityChange = input.QuantityChange,
            QuantityAfter = newQty,
            Reason = input.Reason,
            Notes = input.Notes,
            PerformedById = _current.UserId
        };
        _db.InventoryAdjustments.Add(adj);
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.InventoryEvents,
            new InventoryAdjusted(product.Id, product.StoreId, input.QuantityChange, newQty,
                input.Reason.ToString(), null, DateTime.UtcNow), product.Id.ToString(), ct);

        if (newQty > 0 && newQty <= LowStockThreshold)
            await _kafka.PublishAsync(MarketplaceTopics.InventoryEvents,
                new LowStockWarning(product.Id, product.StoreId, newQty, DateTime.UtcNow), product.Id.ToString(), ct);

        await _audit.LogAsync("inventory.adjust", "Product", product.Id.ToString(), $"{input.QuantityChange:+#;-#;0} ({input.Reason})", ct: ct);
        return new InventoryAdjustmentDto(adj.Id, product.Id, adj.QuantityChange, adj.QuantityAfter,
            adj.Reason, adj.Notes, adj.PerformedById, adj.CreatedAt);
    }

    public async Task<PageResult<InventoryAdjustmentDto>> ListAsync(Guid productId, int page, int pageSize, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        await EnsureOwnerOrAdmin(product, ct);

        var q = _db.InventoryAdjustments.AsNoTracking().Where(a => a.ProductId == productId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new InventoryAdjustmentDto(a.Id, a.ProductId, a.QuantityChange, a.QuantityAfter,
                a.Reason, a.Notes, a.PerformedById, a.CreatedAt))
            .ToListAsync(ct);
        return new PageResult<InventoryAdjustmentDto>(items, total, page, pageSize);
    }

    /// <summary>
    /// Atomic decrement used during order placement. Uses a conditional update so
    /// concurrent checkouts cannot oversell — one of them gets 0 rows affected and
    /// we surface a conflict.
    /// </summary>
    public async Task DecrementForOrderAsync(Guid productId, int quantity, Guid orderId, CancellationToken ct = default)
    {
        if (quantity <= 0) throw new ValidationException(
            new Dictionary<string, string[]> { ["quantity"] = new[] { "Must be > 0." } });

        var rows = await _db.Products
            .Where(p => p.Id == productId && p.StockQuantity >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - quantity), ct);
        if (rows == 0)
            throw new ConflictException($"Insufficient stock for product {productId}.");

        var newQty = await _db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StockQuantity).FirstAsync(ct);
        _db.InventoryAdjustments.Add(new InventoryAdjustment
        {
            ProductId = productId,
            OrderId = orderId,
            QuantityChange = -quantity,
            QuantityAfter = newQty,
            Reason = InventoryReason.Sale,
            PerformedById = _current.UserId
        });
        await _db.SaveChangesAsync(ct);

        var storeId = await _db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StoreId).FirstAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.InventoryEvents,
            new InventoryAdjusted(productId, storeId, -quantity, newQty, "Sale", orderId, DateTime.UtcNow),
            productId.ToString(), ct);
        if (newQty > 0 && newQty <= LowStockThreshold)
            await _kafka.PublishAsync(MarketplaceTopics.InventoryEvents,
                new LowStockWarning(productId, storeId, newQty, DateTime.UtcNow), productId.ToString(), ct);
    }

    private async Task EnsureOwnerOrAdmin(Product p, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (_current.Permissions.Contains(Permissions.Products.Edit)) return;
        var ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == p.StoreId).Select(s => s.OwnerUserId).FirstAsync(ct);
        if (ownerId != uid) throw new ForbiddenException();
    }
}
