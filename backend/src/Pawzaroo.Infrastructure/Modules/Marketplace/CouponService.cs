using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class CouponService : ICouponService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public CouponService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    /// <summary>
    /// Read-only validation used by CartPage to preview the discount before checkout.
    /// The authoritative redemption happens inside OrderService.CheckoutAsync where
    /// the counter is atomically incremented within the order transaction.
    /// </summary>
    public async Task<ApplyCouponResult> ApplyAsync(ApplyCouponInput input, CancellationToken ct = default)
    {
        if (_current.UserId is null) throw new ForbiddenException();
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == input.Code, ct)
            ?? throw new NotFoundException("Coupon", input.Code);
        if (!coupon.IsActive)
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = new[] { "Coupon is inactive." } });
        if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = new[] { "Coupon has expired." } });
        if (input.Subtotal < coupon.MinOrderAmount)
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = new[] { $"Minimum subtotal {coupon.MinOrderAmount:0.00} not met." } });
        if (coupon.MaxRedemptions.HasValue && coupon.RedemptionsCount >= coupon.MaxRedemptions.Value)
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = new[] { "Coupon redemption limit reached." } });

        var discount = coupon.Type == CouponType.Percent
            ? Math.Round(input.Subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : Math.Min(coupon.Value, input.Subtotal);

        return new ApplyCouponResult(coupon.Code, discount, input.Subtotal - discount);
    }

    public async Task<IReadOnlyList<CouponDto>> ListAsync(CancellationToken ct = default)
    {
        EnsureAdmin();
        return await _db.Coupons.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CouponDto(c.Id, c.Code, c.Type, c.Value, c.MinOrderAmount,
                c.MaxRedemptions, c.RedemptionsCount, c.ExpiresAt, c.IsActive, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateAsync(UpsertCouponInput input, CancellationToken ct = default)
    {
        EnsureAdmin();
        var code = NormalizeCode(input.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code, ct))
            throw new ConflictException("A coupon with this code already exists.");

        var coupon = new Coupon
        {
            Code = code,
            Type = input.Type,
            Value = input.Value,
            MinOrderAmount = input.MinOrderAmount,
            MaxRedemptions = input.MaxRedemptions,
            ExpiresAt = input.ExpiresAt,
            IsActive = input.IsActive
        };
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        return coupon.Id;
    }

    public async Task UpdateAsync(Guid id, UpsertCouponInput input, CancellationToken ct = default)
    {
        EnsureAdmin();
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Coupon", id);

        var code = NormalizeCode(input.Code);
        if (code != coupon.Code && await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != id, ct))
            throw new ConflictException("A coupon with this code already exists.");

        coupon.Code = code;
        coupon.Type = input.Type;
        coupon.Value = input.Value;
        coupon.MinOrderAmount = input.MinOrderAmount;
        coupon.MaxRedemptions = input.MaxRedemptions;
        coupon.ExpiresAt = input.ExpiresAt;
        coupon.IsActive = input.IsActive;
        coupon.UpdatedAt = DateTime.UtcNow;
        coupon.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();
        await _db.Coupons.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
    }

    private void EnsureAdmin()
    {
        // Coupons are a site-wide admin construct (not store-scoped). Stores.Approve
        // is the gate the existing admin dashboards already use for cross-cutting
        // marketplace ops; reusing it avoids a new permission for one feature.
        if (!_current.Permissions.Contains(Permissions.Stores.Approve))
            throw new ForbiddenException();
    }

    private static string NormalizeCode(string code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();
}
