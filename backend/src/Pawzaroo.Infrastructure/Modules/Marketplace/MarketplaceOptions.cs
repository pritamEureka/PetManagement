namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class MarketplaceOptions
{
    public const string SectionName = "Marketplace";

    /// <summary>Flat shipping fee added to every order with subtotal below the free-shipping threshold.</summary>
    public decimal ShippingFlatFee { get; set; } = 5m;

    /// <summary>Subtotal at or above which shipping is free. Set to 0 to charge always.</summary>
    public decimal FreeShippingThreshold { get; set; } = 100m;

    /// <summary>Tax expressed as a percent (e.g. 7.5 = 7.5%) applied to subtotal.</summary>
    public decimal TaxPercent { get; set; } = 0m;
}

/// <summary>
/// Single source of truth for shipping + tax math. CartService uses it to
/// preview totals; OrderService.CheckoutAsync uses it to stamp the order.
/// </summary>
public static class FeeCalculator
{
    public static (decimal Shipping, decimal Tax, decimal Total) Compute(decimal subtotal, MarketplaceOptions opts)
    {
        var shipping = (opts.FreeShippingThreshold > 0m && subtotal >= opts.FreeShippingThreshold)
            ? 0m
            : opts.ShippingFlatFee;

        var tax = opts.TaxPercent > 0m
            ? Math.Round(subtotal * opts.TaxPercent / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        return (shipping, tax, subtotal + shipping + tax);
    }
}
