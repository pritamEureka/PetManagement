using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class WishlistService : IWishlistService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public WishlistService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<WishlistItemDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == uid)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WishlistItemDto(
                w.Id, w.ProductId, w.Product.Name,
                w.Product.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).FirstOrDefault(),
                w.Product.Price, w.Product.DiscountPrice, w.Product.StockQuantity,
                w.Product.StoreId, w.Product.Store.Name,
                w.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Guid productId, CancellationToken ct = default)
    {
        var uid = Uid();

        // Idempotent — composite unique index guarantees one row per (user, product).
        // We surface "already saved" as a no-op rather than a 409 because the UX
        // is "heart icon toggles to filled" and a second tap shouldn't bother the user.
        var exists = await _db.WishlistItems.AnyAsync(w => w.UserId == uid && w.ProductId == productId, ct);
        if (exists) return;

        var product = await _db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);
        if (!product) throw new NotFoundException("Product", productId);

        _db.WishlistItems.Add(new WishlistItem { UserId = uid, ProductId = productId });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid productId, CancellationToken ct = default)
    {
        var uid = Uid();
        await _db.WishlistItems.Where(w => w.UserId == uid && w.ProductId == productId).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> IsWishlistedAsync(Guid productId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        if (uid is null) return false;
        return await _db.WishlistItems.AsNoTracking().AnyAsync(w => w.UserId == uid && w.ProductId == productId, ct);
    }
}
