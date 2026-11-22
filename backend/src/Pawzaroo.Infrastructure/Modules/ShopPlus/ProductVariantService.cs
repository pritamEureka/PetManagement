using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

public class ProductVariantService : IProductVariantService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICartService _cart;
    private readonly IMarketplaceCache _cache;

    public ProductVariantService(ApplicationDbContext db, ICurrentUserService current, ICartService cart, IMarketplaceCache cache)
    {
        _db = db;
        _current = current;
        _cart = cart;
        _cache = cache;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<VariantDto>> ListAsync(Guid productId, bool includeInactive = false, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        if (includeInactive) await EnsureCanManageAsync(product, ct);

        var variants = await _db.Set<ProductVariant>().AsNoTracking()
            .Where(v => v.ProductId == productId && (includeInactive || v.IsActive))
            .OrderBy(v => v.Sku).ToListAsync(ct);

        var now = DateTime.UtcNow;
        var ids = variants.Select(v => v.Id).ToList();
        var reserved = await _db.Set<StockReservation>().AsNoTracking()
            .Where(r => r.VariantId != null && ids.Contains(r.VariantId.Value)
                        && r.Status == ReservationStatus.Active && r.ExpiresAt > now)
            .GroupBy(r => r.VariantId!.Value)
            .Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Qty, ct);

        return variants.Select(v => ToDto(v, product, reserved.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<VariantDto> CreateAsync(Guid productId, UpsertVariantInput input, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                      ?? throw new NotFoundException("Product", productId);
        await EnsureCanManageAsync(product, ct);
        Validate(input);

        var sku = input.Sku.Trim();
        if (await _db.Set<ProductVariant>().AnyAsync(v => v.ProductId == productId && v.Sku == sku, ct))
            throw new ConflictException($"Variant SKU '{sku}' already exists for this product.");

        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = sku,
            AttributesJson = JsonSerializer.Serialize(input.Attributes ?? new Dictionary<string, string>()),
            PriceOverride = input.PriceOverride,
            StockQuantity = 0,
            IsActive = input.IsActive,
            CreatedBy = _current.UserId
        };
        _db.Set<ProductVariant>().Add(variant);

        if (input.StockQuantity > 0)
            ApplyStock(product, variant, input.StockQuantity, InventoryReason.Restock, $"Initial stock for variant {sku}");

        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
        return ToDto(variant, product, 0);
    }

    public async Task<VariantDto> UpdateAsync(Guid variantId, UpsertVariantInput input, CancellationToken ct = default)
    {
        var variant = await _db.Set<ProductVariant>().Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId, ct)
                      ?? throw new NotFoundException("ProductVariant", variantId);
        await EnsureCanManageAsync(variant.Product, ct);
        Validate(input);

        var sku = input.Sku.Trim();
        if (sku != variant.Sku && await _db.Set<ProductVariant>().AnyAsync(v => v.ProductId == variant.ProductId && v.Sku == sku && v.Id != variantId, ct))
            throw new ConflictException($"Variant SKU '{sku}' already exists for this product.");

        variant.Sku = sku;
        variant.AttributesJson = JsonSerializer.Serialize(input.Attributes ?? new Dictionary<string, string>());
        variant.PriceOverride = input.PriceOverride;
        variant.IsActive = input.IsActive;
        variant.UpdatedAt = DateTime.UtcNow;
        variant.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
        return ToDto(variant, variant.Product, await ReservedAsync(variantId, ct));
    }

    public async Task DeleteAsync(Guid variantId, CancellationToken ct = default)
    {
        var variant = await _db.Set<ProductVariant>().Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId, ct)
                      ?? throw new NotFoundException("ProductVariant", variantId);
        await EnsureCanManageAsync(variant.Product, ct);

        if (variant.StockQuantity > 0)
            ApplyStock(variant.Product, variant, -variant.StockQuantity, InventoryReason.Adjustment, $"Variant {variant.Sku} removed");
        variant.IsDeleted = true;
        variant.IsActive = false;
        variant.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
    }

    public async Task<VariantDto> AdjustStockAsync(Guid variantId, AdjustVariantStockInput input, CancellationToken ct = default)
    {
        var variant = await _db.Set<ProductVariant>().Include(v => v.Product).FirstOrDefaultAsync(v => v.Id == variantId, ct)
                      ?? throw new NotFoundException("ProductVariant", variantId);
        await EnsureCanManageAsync(variant.Product, ct);
        if (input.QuantityChange == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["quantityChange"] = new[] { "Change must be non-zero." } });
        if (variant.StockQuantity + input.QuantityChange < 0)
            throw new ConflictException("Adjustment would make variant stock negative.");

        ApplyStock(variant.Product, variant, input.QuantityChange, input.Reason, input.Notes ?? $"Variant {variant.Sku} adjustment");
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateProductsAsync(ct);
        return ToDto(variant, variant.Product, await ReservedAsync(variantId, ct));
    }

    public async Task<CartDto> AddToCartAsync(AddVariantToCartInput input, CancellationToken ct = default)
    {
        _ = Uid(); // authenticated users only
        if (input.Quantity <= 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["quantity"] = new[] { "Quantity must be positive." } });

        var variant = await _db.Set<ProductVariant>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.VariantId, ct)
                      ?? throw new NotFoundException("ProductVariant", input.VariantId);
        if (!variant.IsActive) throw new ConflictException("Variant is unavailable.");
        var available = variant.StockQuantity - await ReservedAsync(variant.Id, ct);
        if (available < input.Quantity) throw new ConflictException("Requested quantity exceeds available variant stock.");

        // CartService owns the variant line (separate per variant, variant-priced, linked via CartItemVariant).
        return await _cart.AddAsync(new AddToCartInput(variant.ProductId, input.Quantity, variant.Id), ct);
    }

    // ---------------------------------------------------------------------------

    /// <summary>Moves variant + product stock together and appends an audit row (caller saves).</summary>
    private void ApplyStock(Product product, ProductVariant variant, int change, InventoryReason reason, string? notes)
    {
        variant.StockQuantity += change;
        product.StockQuantity += change;
        _db.InventoryAdjustments.Add(new InventoryAdjustment
        {
            ProductId = product.Id,
            QuantityChange = change,
            QuantityAfter = product.StockQuantity,
            Reason = reason,
            Notes = notes,
            PerformedById = _current.UserId
        });
    }

    private async Task<int> ReservedAsync(Guid variantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.Set<StockReservation>().AsNoTracking()
            .Where(r => r.VariantId == variantId && r.Status == ReservationStatus.Active && r.ExpiresAt > now)
            .SumAsync(r => (int?)r.Quantity, ct) ?? 0;
    }

    private async Task EnsureCanManageAsync(Product p, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (_current.Permissions.Contains(Permissions.Products.Edit)) return;
        var ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == p.StoreId).Select(s => s.OwnerUserId).FirstOrDefaultAsync(ct);
        if (ownerId != uid) throw new ForbiddenException();
    }

    private static void Validate(UpsertVariantInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Sku) || input.Sku.Trim().Length > 64) errors["sku"] = new[] { "SKU is required (max 64 chars)." };
        if (input.PriceOverride is < 0) errors["priceOverride"] = new[] { "Price cannot be negative." };
        if (input.StockQuantity < 0) errors["stockQuantity"] = new[] { "Stock cannot be negative." };
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static VariantDto ToDto(ProductVariant v, Product p, int reserved)
    {
        Dictionary<string, string> attrs;
        try { attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(v.AttributesJson) ?? new(); }
        catch (JsonException) { attrs = new(); }
        var effective = v.PriceOverride ?? p.DiscountPrice ?? p.Price;
        return new VariantDto(v.Id, v.ProductId, v.Sku, attrs, v.PriceOverride, effective,
            v.StockQuantity, reserved, Math.Max(0, v.StockQuantity - reserved), v.IsActive);
    }
}
