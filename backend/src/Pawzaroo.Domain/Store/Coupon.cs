using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Store;

public enum CouponType { Percent = 0, Fixed = 1 }

/// <summary>
/// Site-wide discount coupon. <see cref="Code"/> is the unique entry key.
/// <see cref="MinOrderAmount"/>, <see cref="ExpiresAt"/>, and the redemption
/// counters together gate validity. We don't model per-user redemption caps in
/// this MVP — the cap is global via <see cref="MaxRedemptions"/>.
/// </summary>
public class Coupon : AuditableEntity
{
    public string Code { get; set; } = default!;
    public CouponType Type { get; set; }

    /// <summary>For Percent: 0–100. For Fixed: amount in the order currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Cart subtotal must be ≥ this to apply. 0 disables the floor.</summary>
    public decimal MinOrderAmount { get; set; }

    /// <summary>Null = unlimited. The atomic increment in CheckoutAsync guards against over-redemption.</summary>
    public int? MaxRedemptions { get; set; }
    public int RedemptionsCount { get; set; }

    /// <summary>Null = no expiry.</summary>
    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; } = true;
}
