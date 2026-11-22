using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

public class ProductSubscriptionService : IProductSubscriptionService, IReorderService
{
    /// <summary>Subscribe-and-save discount applied to every recurring order.</summary>
    public const decimal DefaultDiscountPercent = 10m;
    private static readonly string[] AllowedPayment = { "cod", "sslcommerz" };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICartService _cart;

    public ProductSubscriptionService(ApplicationDbContext db, ICurrentUserService current, ICartService cart)
    {
        _db = db;
        _current = current;
        _cart = cart;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<SubscriptionDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var subs = await _db.Set<ProductSubscription>().AsNoTracking().Include(s => s.Product).ThenInclude(p => p.Images)
            .Where(s => s.UserId == uid && s.Status != SubscriptionStatus.Cancelled)
            .OrderBy(s => s.NextRunAt).ToListAsync(ct);
        return await MapAsync(subs, ct);
    }

    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var errors = new Dictionary<string, string[]>();
        if (input.Quantity < 1 || input.Quantity > 50) errors["quantity"] = new[] { "Quantity must be 1-50." };
        if (input.IntervalDays < 7 || input.IntervalDays > 365) errors["intervalDays"] = new[] { "Interval must be 7-365 days." };
        var method = (input.PaymentMethod ?? "cod").Trim().ToLowerInvariant();
        if (!AllowedPayment.Contains(method)) errors["paymentMethod"] = new[] { "Use 'cod' or 'sslcommerz'." };
        if (errors.Count > 0) throw new ValidationException(errors);

        var product = await _db.Products.Include(p => p.Images).Include(p => p.Store)
                          .FirstOrDefaultAsync(p => p.Id == input.ProductId, ct)
                      ?? throw new NotFoundException("Product", input.ProductId);
        if (!product.IsActive || product.Store.ApprovalStatus != ApprovalStatus.Approved)
            throw new ConflictException("Product is unavailable.");

        if (input.VariantId is { } vid)
        {
            var ok = await _db.Set<ProductVariant>().AnyAsync(v => v.Id == vid && v.ProductId == product.Id && v.IsActive, ct);
            if (!ok) throw new NotFoundException("ProductVariant", vid);
        }
        var addrOk = await _db.ShippingAddresses.AnyAsync(a => a.Id == input.ShippingAddressId && a.UserId == uid, ct);
        if (!addrOk) throw new NotFoundException("ShippingAddress", input.ShippingAddressId);

        var dupe = await _db.Set<ProductSubscription>().AnyAsync(s => s.UserId == uid && s.ProductId == input.ProductId
            && s.VariantId == input.VariantId && s.Status != SubscriptionStatus.Cancelled, ct);
        if (dupe) throw new ConflictException("You already subscribe to this item.");

        var sub = new ProductSubscription
        {
            UserId = uid, ProductId = product.Id, Product = product, VariantId = input.VariantId,
            Quantity = input.Quantity, IntervalDays = input.IntervalDays,
            DiscountPercent = DefaultDiscountPercent,
            NextRunAt = DateTime.UtcNow.AddDays(input.IntervalDays),
            ShippingAddressId = input.ShippingAddressId, PaymentMethod = method, CreatedBy = uid
        };
        _db.Set<ProductSubscription>().Add(sub);
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new[] { sub }, ct))[0];
    }

    public async Task<SubscriptionDto> UpdateAsync(Guid id, UpdateSubscriptionInput input, CancellationToken ct = default)
    {
        var sub = await GetMineAsync(id, ct);
        if (input.Quantity is { } q)
        {
            if (q < 1 || q > 50) throw new ValidationException(new Dictionary<string, string[]> { ["quantity"] = new[] { "Quantity must be 1-50." } });
            sub.Quantity = q;
        }
        if (input.IntervalDays is { } d)
        {
            if (d < 7 || d > 365) throw new ValidationException(new Dictionary<string, string[]> { ["intervalDays"] = new[] { "Interval must be 7-365 days." } });
            sub.IntervalDays = d;
        }
        if (input.ShippingAddressId is { } a)
        {
            var uid = Uid();
            if (!await _db.ShippingAddresses.AnyAsync(x => x.Id == a && x.UserId == uid, ct)) throw new NotFoundException("ShippingAddress", a);
            sub.ShippingAddressId = a;
        }
        if (!string.IsNullOrWhiteSpace(input.PaymentMethod))
        {
            var m = input.PaymentMethod.Trim().ToLowerInvariant();
            if (!AllowedPayment.Contains(m)) throw new ValidationException(new Dictionary<string, string[]> { ["paymentMethod"] = new[] { "Use 'cod' or 'sslcommerz'." } });
            sub.PaymentMethod = m;
        }
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new[] { sub }, ct))[0];
    }

    public async Task<SubscriptionDto> PauseAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await GetMineAsync(id, ct);
        sub.Status = SubscriptionStatus.Paused;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new[] { sub }, ct))[0];
    }

    public async Task<SubscriptionDto> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await GetMineAsync(id, ct);
        sub.Status = SubscriptionStatus.Active;
        sub.FailureCount = 0;
        sub.LastError = null;
        if (sub.NextRunAt < DateTime.UtcNow) sub.NextRunAt = DateTime.UtcNow.AddDays(1);
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new[] { sub }, ct))[0];
    }

    public async Task<SubscriptionDto> SkipNextAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await GetMineAsync(id, ct);
        sub.NextRunAt = sub.NextRunAt.AddDays(sub.IntervalDays);
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(new[] { sub }, ct))[0];
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        var sub = await GetMineAsync(id, ct);
        sub.Status = SubscriptionStatus.Cancelled;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ---- reorder ---------------------------------------------------------------

    public async Task<ReorderResultDto> ReorderAsync(Guid orderId, CancellationToken ct = default)
    {
        var uid = Uid();
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).ThenInclude(i => i.Product)
                        .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == uid, ct)
                    ?? throw new NotFoundException("Order", orderId);

        var skipped = new List<ReorderSkippedDto>();
        int added = 0;
        foreach (var item in order.Items)
        {
            try
            {
                await _cart.AddAsync(new AddToCartInput(item.ProductId, item.Quantity), ct);
                added++;
            }
            catch (ConflictException ex)
            {
                skipped.Add(new ReorderSkippedDto(item.ProductId, item.Product.Name, ex.Message));
            }
            catch (NotFoundException ex)
            {
                skipped.Add(new ReorderSkippedDto(item.ProductId, item.Product.Name, ex.Message));
            }
        }
        return new ReorderResultDto(added, skipped, await _cart.GetMineAsync(ct));
    }

    // ---- helpers ---------------------------------------------------------------

    private async Task<ProductSubscription> GetMineAsync(Guid id, CancellationToken ct)
    {
        var uid = Uid();
        var sub = await _db.Set<ProductSubscription>().Include(s => s.Product).ThenInclude(p => p.Images)
                      .FirstOrDefaultAsync(s => s.Id == id && s.UserId == uid, ct)
                  ?? throw new NotFoundException("Subscription", id);
        if (sub.Status == SubscriptionStatus.Cancelled) throw new ConflictException("Subscription is cancelled.");
        return sub;
    }

    private async Task<IReadOnlyList<SubscriptionDto>> MapAsync(IEnumerable<ProductSubscription> subs, CancellationToken ct)
    {
        var list = subs.ToList();
        var vids = list.Where(s => s.VariantId != null).Select(s => s.VariantId!.Value).Distinct().ToList();
        var variants = vids.Count == 0
            ? new Dictionary<Guid, ProductVariant>()
            : await _db.Set<ProductVariant>().AsNoTracking().Where(v => vids.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

        return list.Select(s =>
        {
            variants.TryGetValue(s.VariantId ?? Guid.Empty, out var v);
            var unit = v?.PriceOverride ?? s.Product.DiscountPrice ?? s.Product.Price;
            var disc = Math.Round(unit * (1 - s.DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
            var img = s.Product.Images.OrderBy(i => i.OrderIndex).Select(i => i.Url).FirstOrDefault();
            return new SubscriptionDto(s.Id, s.ProductId, s.Product.Name, img, s.VariantId, v?.Sku,
                s.Quantity, s.IntervalDays, s.DiscountPercent, unit, disc, s.NextRunAt, s.Status, s.FailureCount,
                s.LastError, s.LastOrderId, s.LastRunAt, s.ShippingAddressId, s.PaymentMethod, s.CreatedAt);
        }).ToList();
    }
}
