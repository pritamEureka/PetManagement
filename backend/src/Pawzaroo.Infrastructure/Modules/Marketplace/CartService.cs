using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly MarketplaceOptions _market;
    private readonly Pawzaroo.Application.Modules.LogisticsPlus.IShippingQuoteService? _shippingQuote; // optional zone preview

    public CartService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        IOptions<MarketplaceOptions> market,
        Pawzaroo.Application.Modules.LogisticsPlus.IShippingQuoteService? shippingQuote = null)
    {
        _shippingQuote = shippingQuote;
        _db = db;
        _current = current;
        _kafka = kafka;
        _market = market.Value;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public Task<CartDto> GetMineAsync(CancellationToken ct = default) => GetMineAsync(null, ct);

    public async Task<CartDto> GetMineAsync(string? city, CancellationToken ct)
    {
        var uid = Uid();
        var cart = await EnsureCartAsync(uid, ct);
        return await BuildDto(cart.Id, uid, ct, city);
    }

    private async Task<string?> DefaultCityAsync(Guid uid, CancellationToken ct) =>
        _shippingQuote is null ? null
            : await _db.ShippingAddresses.AsNoTracking().Where(a => a.UserId == uid && a.IsDefault)
                .Select(a => a.City).FirstOrDefaultAsync(ct);

    public async Task<CartDto> AddAsync(AddToCartInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var cart = await EnsureCartAsync(uid, ct);

        // The stock read here is advisory (UX guard against obviously-overpicking
        // when the catalog still shows stock). It is intentionally NOT race-free.
        // The canonical, race-free decrement happens at checkout via
        // InventoryService.DecrementForOrderAsync, which uses a conditional
        // ExecuteUpdateAsync (UPDATE ... WHERE stock_quantity >= @qty) so the
        // last writer cannot oversell. Don't tighten this into a row lock here —
        // the cart is long-lived and holding pg locks across user sessions would
        // be worse than the occasional cart-vs-stock discrepancy.
        var product = await _db.Products.AsNoTracking().Where(p => p.Id == input.ProductId)
            .Select(p => new { p.Id, p.StoreId, p.IsActive, p.StockQuantity, p.Price, p.DiscountPrice }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Product", input.ProductId);
        if (!product.IsActive) throw new ConflictException("Product is unavailable.");

        // Variant lines are priced/stocked by the variant and kept separate from other lines of the product.
        ProductVariant? variant = null;
        if (input.VariantId is { } vid)
        {
            variant = await _db.Set<ProductVariant>().AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vid && v.ProductId == product.Id, ct)
                ?? throw new NotFoundException("ProductVariant", vid);
            if (!variant.IsActive) throw new ConflictException("Variant is unavailable.");
        }
        var stock = variant?.StockQuantity ?? product.StockQuantity;
        var unitPrice = variant?.PriceOverride ?? product.DiscountPrice ?? product.Price;
        if (stock < input.Quantity)
            throw new ConflictException("Requested quantity exceeds available stock.");

        var lines = await _db.CartItems.Where(i => i.UserId == uid && i.ProductId == input.ProductId).ToListAsync(ct);
        var lineIds = lines.Select(l => l.Id).ToList();
        var lineLinks = await _db.Set<CartItemVariant>().Where(l => lineIds.Contains(l.CartItemId)).ToListAsync(ct);
        var existing = lines.FirstOrDefault(l => lineLinks.FirstOrDefault(k => k.CartItemId == l.Id)?.VariantId == variant?.Id);
        if (existing is null)
        {
            existing = new CartItem
            {
                CartId = cart.Id,
                UserId = uid,
                ProductId = product.Id,
                Quantity = input.Quantity,
                UnitPriceSnapshot = unitPrice
            };
            _db.CartItems.Add(existing);
            if (variant is not null)
                _db.Set<CartItemVariant>().Add(new CartItemVariant { CartItemId = existing.Id, VariantId = variant.Id });
        }
        else
        {
            existing.Quantity += input.Quantity;
            existing.UnitPriceSnapshot = unitPrice;
            if (existing.Quantity > stock)
                throw new ConflictException("Requested quantity exceeds available stock.");
        }

        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new CartItemAdded(uid, cart.Id, product.Id, input.Quantity, DateTime.UtcNow), uid.ToString(), ct);
        return await BuildDto(cart.Id, uid, ct);
    }

    public async Task<CartDto> UpdateItemAsync(Guid cartItemId, UpdateCartItemInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var item = await _db.CartItems.Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == cartItemId && i.UserId == uid, ct)
            ?? throw new NotFoundException("CartItem", cartItemId);

        if (input.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
            await RemoveLinksAsync(new[] { item.Id }, ct);
        }
        else
        {
            var lineStock = await LineStockAsync(item, ct);
            if (input.Quantity > lineStock)
                throw new ConflictException("Requested quantity exceeds available stock.");
            item.Quantity = input.Quantity;
        }
        await _db.SaveChangesAsync(ct);
        await PruneBundleMarksAsync(uid, ct);
        return await BuildDto(item.CartId, uid, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct = default)
    {
        var uid = Uid();
        var item = await _db.CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.UserId == uid, ct)
                   ?? throw new NotFoundException("CartItem", cartItemId);
        _db.CartItems.Remove(item);
        await RemoveLinksAsync(new[] { item.Id }, ct);
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new CartItemRemoved(uid, item.CartId, item.ProductId, DateTime.UtcNow), uid.ToString(), ct);
        await PruneBundleMarksAsync(uid, ct);
        return await BuildDto(item.CartId, uid, ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var items = _db.CartItems.Where(i => i.UserId == uid);
        var cartId = await items.Select(i => i.CartId).FirstOrDefaultAsync(ct);
        await RemoveLinksAsync(await items.Select(i => i.Id).ToListAsync(ct), ct);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);
        await PruneBundleMarksAsync(uid, ct);
        if (cartId != Guid.Empty)
            await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
                new CartCleared(uid, cartId, DateTime.UtcNow), uid.ToString(), ct);
    }


    /// <summary>
    /// Keeps BundleCartDiscount rows honest: the bundle discount is only valid while the cart still holds the
    /// component quantities (loyalty-plus). Rows no longer covered are reduced/removed.
    /// </summary>
    private async Task PruneBundleMarksAsync(Guid uid, CancellationToken ct)
    {
        var marks = await _db.Set<BundleCartDiscount>().Where(m => m.UserId == uid).ToListAsync(ct);
        if (marks.Count == 0) return;
        var bundleIds = marks.Select(m => m.BundleId).ToList();
        var bundles = await _db.Set<ProductBundle>().AsNoTracking().Include(b => b.Items).Where(b => bundleIds.Contains(b.Id)).ToListAsync(ct);
        var lines = await _db.CartItems.AsNoTracking().Where(i => i.UserId == uid).Select(i => new { i.Id, i.ProductId, i.Quantity }).ToListAsync(ct);
        var lineIds = lines.Select(l => l.Id).ToList();
        var links = await _db.Set<CartItemVariant>().AsNoTracking().Where(l => lineIds.Contains(l.CartItemId))
            .ToDictionaryAsync(l => l.CartItemId, l => l.VariantId, ct);
        var pool = lines.GroupBy(l => (l.ProductId, VariantId: links.TryGetValue(l.Id, out var v) ? (Guid?)v : null))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var m in marks)
        {
            var b = bundles.FirstOrDefault(x => x.Id == m.BundleId);
            var covered = b is null ? 0 : BundlePricing.BundlesCoveredByCart(m.Quantity,
                b.Items.Select(i => (i.Quantity, pool.GetValueOrDefault((i.ProductId, i.VariantId)))));
            if (covered <= 0) _db.Set<BundleCartDiscount>().Remove(m);
            else if (covered < m.Quantity) m.Quantity = covered;
        }
        await _db.SaveChangesAsync(ct);
    }
    private async Task RemoveLinksAsync(IReadOnlyCollection<Guid> cartItemIds, CancellationToken ct)
    {
        if (cartItemIds.Count == 0) return;
        var links = await _db.Set<CartItemVariant>().Where(l => cartItemIds.Contains(l.CartItemId)).ToListAsync(ct);
        _db.Set<CartItemVariant>().RemoveRange(links);
    }

    /// <summary>Stock the line is bound by: its variant's stock when it has one, else the product's.</summary>
    private async Task<int> LineStockAsync(CartItem item, CancellationToken ct)
    {
        var vid = await _db.Set<CartItemVariant>().AsNoTracking().Where(l => l.CartItemId == item.Id)
            .Select(l => (Guid?)l.VariantId).FirstOrDefaultAsync(ct);
        if (vid is null) return item.Product.StockQuantity;
        var stock = await _db.Set<ProductVariant>().AsNoTracking().Where(v => v.Id == vid)
            .Select(v => (int?)v.StockQuantity).FirstOrDefaultAsync(ct);
        return stock ?? 0;
    }

    private static string VariantLabel(ProductVariant v)
    {
        try
        {
            var attrs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v.AttributesJson);
            if (attrs is { Count: > 0 }) return string.Join(" / ", attrs.Values);
        }
        catch (System.Text.Json.JsonException) { }
        return v.Sku;
    }

    private async Task<Cart> EnsureCartAsync(Guid uid, CancellationToken ct)
    {
        var cart = await _db.Carts.FirstOrDefaultAsync(c => c.UserId == uid && c.Status == CartStatus.Active, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = uid, Status = CartStatus.Active };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync(ct);
        }
        return cart;
    }

    private async Task<CartDto> BuildDto(Guid cartId, Guid uid, CancellationToken ct, string? city = null)
    {
        var rows = await _db.CartItems.AsNoTracking().Where(i => i.UserId == uid)
            .Select(i => new
            {
                i.Id, i.ProductId, ProductName = i.Product.Name,
                Image = i.Product.Images.OrderBy(im => im.OrderIndex).Select(im => im.Url).FirstOrDefault(),
                i.Product.StoreId, StoreName = i.Product.Store.Name,
                i.Quantity,
                Unit = i.Product.DiscountPrice ?? i.Product.Price,
                Stock = i.Product.StockQuantity
            })
            .ToListAsync(ct);

        var rowIds = rows.Select(r => r.Id).ToList();
        var links = await _db.Set<CartItemVariant>().AsNoTracking()
            .Where(l => rowIds.Contains(l.CartItemId)).ToDictionaryAsync(l => l.CartItemId, l => l.VariantId, ct);
        var variantIds = links.Values.Distinct().ToList();
        var variants = variantIds.Count == 0
            ? new Dictionary<Guid, ProductVariant>()
            : await _db.Set<ProductVariant>().AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

        var items = rows.Select(r =>
        {
            ProductVariant? v = null;
            if (links.TryGetValue(r.Id, out var vid)) variants.TryGetValue(vid, out v);
            var unit = v?.PriceOverride ?? r.Unit;
            return new CartItemDto(r.Id, r.ProductId, r.ProductName, r.Image, r.StoreId, r.StoreName,
                r.Quantity, unit, unit * r.Quantity, v?.StockQuantity ?? r.Stock,
                v?.Id, v is null ? null : VariantLabel(v));
        }).ToList();

        var currency = await _db.Carts.AsNoTracking().Where(c => c.Id == cartId).Select(c => c.Currency).FirstOrDefaultAsync(ct) ?? "USD";
        var subtotal = items.Sum(i => i.Total);
        var (shipping, tax, total) = FeeCalculator.Compute(subtotal, _market);
        // Preview with delivery-zone fees when the destination city is known and every store resolves to a zone.
        city ??= await DefaultCityAsync(uid, ct);
        if (_shippingQuote is not null && !string.IsNullOrWhiteSpace(city) && items.Count > 0)
        {
            var perStore = items.GroupBy(i => i.StoreId).Select(g => (g.Key, g.Sum(x => x.Total))).ToList();
            var zoneFee = await _shippingQuote.TryQuoteOrderFeeAsync(perStore, city, DateTime.UtcNow, ct);
            if (zoneFee.HasValue) { total = total - shipping + zoneFee.Value; shipping = zoneFee.Value; }
        }
        return new CartDto(cartId, currency, items, subtotal, items.Sum(i => i.Quantity), shipping, tax, total);
    }
}
