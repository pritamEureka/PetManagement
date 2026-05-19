using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
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

    public CartService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        IOptions<MarketplaceOptions> market)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _market = market.Value;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<CartDto> GetMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var cart = await EnsureCartAsync(uid, ct);
        return await BuildDto(cart.Id, uid, ct);
    }

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
        if (product.StockQuantity < input.Quantity)
            throw new ConflictException("Requested quantity exceeds available stock.");

        var existing = await _db.CartItems.FirstOrDefaultAsync(i => i.UserId == uid && i.ProductId == input.ProductId, ct);
        if (existing is null)
        {
            existing = new CartItem
            {
                CartId = cart.Id,
                UserId = uid,
                ProductId = product.Id,
                Quantity = input.Quantity,
                UnitPriceSnapshot = product.DiscountPrice ?? product.Price
            };
            _db.CartItems.Add(existing);
        }
        else
        {
            existing.Quantity += input.Quantity;
            if (existing.Quantity > product.StockQuantity)
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
        }
        else
        {
            if (input.Quantity > item.Product.StockQuantity)
                throw new ConflictException("Requested quantity exceeds available stock.");
            item.Quantity = input.Quantity;
        }
        await _db.SaveChangesAsync(ct);
        return await BuildDto(item.CartId, uid, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartItemId, CancellationToken ct = default)
    {
        var uid = Uid();
        var item = await _db.CartItems.FirstOrDefaultAsync(i => i.Id == cartItemId && i.UserId == uid, ct)
                   ?? throw new NotFoundException("CartItem", cartItemId);
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new CartItemRemoved(uid, item.CartId, item.ProductId, DateTime.UtcNow), uid.ToString(), ct);
        return await BuildDto(item.CartId, uid, ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var items = _db.CartItems.Where(i => i.UserId == uid);
        var cartId = await items.Select(i => i.CartId).FirstOrDefaultAsync(ct);
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync(ct);
        if (cartId != Guid.Empty)
            await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
                new CartCleared(uid, cartId, DateTime.UtcNow), uid.ToString(), ct);
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

    private async Task<CartDto> BuildDto(Guid cartId, Guid uid, CancellationToken ct)
    {
        var items = await _db.CartItems.AsNoTracking().Where(i => i.UserId == uid)
            .Select(i => new CartItemDto(
                i.Id, i.ProductId, i.Product.Name,
                i.Product.Images.OrderBy(im => im.OrderIndex).Select(im => im.Url).FirstOrDefault(),
                i.Product.StoreId, i.Product.Store.Name,
                i.Quantity,
                i.Product.DiscountPrice ?? i.Product.Price,
                (i.Product.DiscountPrice ?? i.Product.Price) * i.Quantity,
                i.Product.StockQuantity))
            .ToListAsync(ct);

        var currency = await _db.Carts.AsNoTracking().Where(c => c.Id == cartId).Select(c => c.Currency).FirstOrDefaultAsync(ct) ?? "USD";
        var subtotal = items.Sum(i => i.Total);
        var (shipping, tax, total) = FeeCalculator.Compute(subtotal, _market);
        return new CartDto(cartId, currency, items, subtotal, items.Sum(i => i.Quantity), shipping, tax, total);
    }
}
