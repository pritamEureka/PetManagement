using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Application.Modules.ShopPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.ShopPlus;

/// <summary>
/// Processes due ProductSubscriptions. Orders are created ONLY through the public
/// <see cref="IOrderService.CheckoutAsync"/>, composed like this per subscription:
///   1. (prep scope) park the subscriber's own cart lines, put the subscription line in the cart and
///      create a single-use coupon carrying the subscribe-and-save discount (+ any variant price reduction);
///   2. (order scope, acting as the subscriber via ShopPlusUserContext) IOrderService.CheckoutAsync;
///   3. (restore scope) always put the parked cart lines back and retire the coupon.
/// Failures back off 1h/4h/16h and the subscription is paused on the 3rd consecutive failure.
/// Non-COD orders still unpaid after <see cref="PaymentGrace"/> count as a missed payment.
/// </summary>
public class SubscriptionRunner : ISubscriptionRunner
{
    public const int MaxFailures = 3;
    public static readonly TimeSpan PaymentGrace = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SubscriptionRunner> _logger;

    public SubscriptionRunner(IServiceScopeFactory scopes, ILogger<SubscriptionRunner> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task<SubscriptionRunSummary> RunDueAsync(CancellationToken ct = default)
    {
        await CheckMissedPaymentsAsync(ct);

        List<Guid> dueIds;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            dueIds = await db.Set<ProductSubscription>().AsNoTracking()
                .Where(s => s.Status == SubscriptionStatus.Active && s.NextRunAt <= now)
                .OrderBy(s => s.NextRunAt).Select(s => s.Id).Take(100).ToListAsync(ct);
        }

        int ok = 0, failed = 0, paused = 0;
        foreach (var id in dueIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var outcome = await ProcessOneAsync(id, ct);
                if (outcome == Outcome.Succeeded) ok++;
                else if (outcome == Outcome.Failed) failed++;
                else if (outcome == Outcome.Paused) { failed++; paused++; }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription {Id} processing crashed", id);
            }
        }
        return new SubscriptionRunSummary(dueIds.Count, ok, failed, paused);
    }

    private enum Outcome { Skipped, Succeeded, Failed, Paused }

    private async Task<Outcome> ProcessOneAsync(Guid subId, CancellationToken ct)
    {
        ProductSubscription sub;
        Product product;
        ProductVariant? variant = null;
        string? precheckError = null;

        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var loaded = await db.Set<ProductSubscription>().AsNoTracking().Include(s => s.Product).ThenInclude(p => p.Store)
                .FirstOrDefaultAsync(s => s.Id == subId, ct);
            if (loaded is null || loaded.Status != SubscriptionStatus.Active || loaded.NextRunAt > DateTime.UtcNow) return Outcome.Skipped;
            sub = loaded;
            product = loaded.Product;
            if (sub.VariantId is { } vid)
                variant = await db.Set<ProductVariant>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid, ct);

            if (!product.IsActive || product.Store.ApprovalStatus != ApprovalStatus.Approved)
                precheckError = "Product is no longer available.";
            else if (sub.VariantId != null && (variant is null || !variant.IsActive))
                precheckError = "Selected variant is no longer available.";
            else if ((variant?.StockQuantity ?? product.StockQuantity) < sub.Quantity)
                precheckError = "Not enough stock.";
        }

        if (precheckError is not null) return await FailAsync(subId, precheckError, ct);

        OrderDto? order = null;
        string? error = null;
        try
        {
            order = await PlaceOrderAsync(sub, product, variant, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            error = ex.Message;
            _logger.LogWarning(ex, "Subscription {Id} order failed", subId);
        }

        if (order is null) return await FailAsync(subId, error ?? "Order could not be placed.", ct);

        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var s = await db.Set<ProductSubscription>().FirstAsync(x => x.Id == subId, ct);
            var now = DateTime.UtcNow;
            var next = s.NextRunAt.AddDays(s.IntervalDays);
            while (next <= now) next = next.AddDays(s.IntervalDays);
            s.NextRunAt = next;
            s.FailureCount = 0;
            s.LastError = null;
            s.LastOrderId = order.Id;
            s.LastRunAt = now;
            s.UpdatedAt = now;
            db.Set<InAppNotification>().Add(new InAppNotification
            {
                UserId = s.UserId,
                Title = "Your subscription order was placed",
                Body = $"{product.Name} x{s.Quantity} - order {order.OrderNumber}." +
                       (order.PaymentCheckoutUrl is null ? "" : " Complete payment to confirm it."),
                Url = order.PaymentCheckoutUrl ?? $"/orders/{order.Id}"
            });
            await db.SaveChangesAsync(ct);
        }
        return Outcome.Succeeded;
    }

    private async Task<OrderDto> PlaceOrderAsync(ProductSubscription sub, Product product, ProductVariant? variant, CancellationToken ct)
    {
        var uid = sub.UserId;
        var baseUnit = product.DiscountPrice ?? product.Price;
        var variantUnit = variant?.PriceOverride ?? baseUnit;
        // Checkout charges the variant price itself (CartItemVariant link), so the coupon carries only the subscribe-and-save discount.
        var subDiscount = Math.Round(variantUnit * sub.Quantity * sub.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var couponValue = subDiscount;

        var parked = new List<ParkedLine>();
        string? couponCode = null;
        Guid couponId = Guid.Empty;

        // 1. prep: park the user's cart, add our line, create the coupon
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cart = await db.Set<Cart>().FirstOrDefaultAsync(c => c.UserId == uid && c.Status == CartStatus.Active, ct);
            if (cart is null)
            {
                cart = new Cart { UserId = uid, Status = CartStatus.Active };
                db.Set<Cart>().Add(cart);
                await db.SaveChangesAsync(ct);
            }

            var existing = await db.CartItems.Where(c => c.UserId == uid).ToListAsync(ct);
            var existingIds = existing.Select(e => e.Id).ToList();
            var links = await db.Set<CartItemVariant>().Where(l => existingIds.Contains(l.CartItemId)).ToListAsync(ct);
            foreach (var e in existing)
                parked.Add(new ParkedLine(e.CartId, e.ProductId, e.Quantity, e.UnitPriceSnapshot, e.AddedAt,
                    links.FirstOrDefault(l => l.CartItemId == e.Id)?.VariantId));
            db.Set<CartItemVariant>().RemoveRange(links);
            db.CartItems.RemoveRange(existing);

            var subLine = new CartItem
            {
                CartId = cart.Id, UserId = uid, ProductId = product.Id,
                Quantity = sub.Quantity, UnitPriceSnapshot = variantUnit
            };
            db.CartItems.Add(subLine);
            if (variant is not null)
                db.Set<CartItemVariant>().Add(new CartItemVariant { CartItemId = subLine.Id, VariantId = variant.Id });

            if (couponValue > 0m)
            {
                couponCode = "SUB" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
                var coupon = new Coupon
                {
                    Code = couponCode, Type = CouponType.Fixed, Value = couponValue,
                    MinOrderAmount = 0m, MaxRedemptions = 1, ExpiresAt = DateTime.UtcNow.AddHours(1), IsActive = true
                };
                db.Coupons.Add(coupon);
                couponId = coupon.Id;
            }
            await db.SaveChangesAsync(ct);
        }

        try
        {
            // 2. place the order through the public service, acting as the subscriber
            using var orderScope = _scopes.CreateScope();
            orderScope.ServiceProvider.GetRequiredService<ShopPlusUserContext>().OverrideUserId = uid;
            var orders = orderScope.ServiceProvider.GetRequiredService<IOrderService>();
            var order = await orders.CheckoutAsync(
                new CheckoutInput(sub.ShippingAddressId, null, null, null, sub.PaymentMethod, couponCode), ct);

            return order;
        }
        finally
        {
            // 3. restore the subscriber's own cart no matter what happened
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var leftovers = await db.CartItems.Where(c => c.UserId == uid).ToListAsync(CancellationToken.None);
            var leftoverIds = leftovers.Select(l => l.Id).ToList();
            var leftoverLinks = await db.Set<CartItemVariant>().Where(l => leftoverIds.Contains(l.CartItemId)).ToListAsync(CancellationToken.None);
            db.Set<CartItemVariant>().RemoveRange(leftoverLinks);
            db.CartItems.RemoveRange(leftovers);
            await db.SaveChangesAsync(CancellationToken.None);

            foreach (var p in parked)
            {
                var item = new CartItem
                {
                    CartId = p.CartId, UserId = uid, ProductId = p.ProductId, Quantity = p.Quantity,
                    UnitPriceSnapshot = p.Unit, AddedAt = p.AddedAt
                };
                db.CartItems.Add(item);
                if (p.VariantId is { } v)
                    db.Set<CartItemVariant>().Add(new CartItemVariant { CartItemId = item.Id, VariantId = v });
            }
            if (couponId != Guid.Empty)
            {
                var c = await db.Coupons.FirstOrDefaultAsync(x => x.Id == couponId, CancellationToken.None);
                if (c is not null) c.IsActive = false;
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private sealed record ParkedLine(Guid CartId, Guid ProductId, int Quantity, decimal Unit, DateTime AddedAt, Guid? VariantId);

    /// <summary>Records a failure, applies backoff, pauses on the 3rd consecutive failure, notifies the user.</summary>
    private async Task<Outcome> FailAsync(Guid subId, string reason, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var s = await db.Set<ProductSubscription>().Include(x => x.Product).FirstAsync(x => x.Id == subId, ct);
        var now = DateTime.UtcNow;

        s.FailureCount += 1;
        s.LastError = reason.Length > 480 ? reason[..480] : reason;
        s.LastRunAt = now;
        s.UpdatedAt = now;

        bool pause = s.FailureCount >= MaxFailures;
        if (pause) s.Status = SubscriptionStatus.Paused;
        else s.NextRunAt = now.AddHours(Math.Pow(4, s.FailureCount - 1)); // 1h, 4h

        db.Set<InAppNotification>().Add(new InAppNotification
        {
            UserId = s.UserId,
            Title = pause ? "Subscription paused" : "Subscription order failed",
            Body = pause
                ? $"We couldn't process '{s.Product.Name}' after {MaxFailures} attempts ({reason}). Resume it when ready."
                : $"We couldn't process '{s.Product.Name}' ({reason}). We'll retry automatically.",
            Url = "/my-subscriptions"
        });
        await db.SaveChangesAsync(ct);
        return pause ? Outcome.Paused : Outcome.Failed;
    }

    /// <summary>Non-COD orders from subscriptions that stay unpaid past the grace window are cancelled and counted as a failure.</summary>
    private async Task CheckMissedPaymentsAsync(CancellationToken ct)
    {
        List<(Guid SubId, Guid OrderId, Guid UserId)> missed;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoff = DateTime.UtcNow - PaymentGrace;
            var rows = await (from s in db.Set<ProductSubscription>().AsNoTracking()
                              join o in db.Orders.AsNoTracking() on s.LastOrderId equals o.Id
                              where s.Status == SubscriptionStatus.Active && s.PaymentMethod != "cod"
                                    && o.PaymentStatus != PaymentStatus.Paid && o.Status == OrderStatus.Created
                                    && o.CreatedAt < cutoff
                              select new { s.Id, OrderId = o.Id, s.UserId }).Take(100).ToListAsync(ct);
            missed = rows.Select(r => (r.Id, r.OrderId, r.UserId)).ToList();
        }

        foreach (var (subId, orderId, userId) in missed)
        {
            try
            {
                using (var orderScope = _scopes.CreateScope())
                {
                    orderScope.ServiceProvider.GetRequiredService<ShopPlusUserContext>().OverrideUserId = userId;
                    var orders = orderScope.ServiceProvider.GetRequiredService<IOrderService>();
                    await orders.CancelAsync(orderId, "Subscription payment not received", ct);
                }
                using (var scope = _scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var s = await db.Set<ProductSubscription>().FirstAsync(x => x.Id == subId, ct);
                    s.LastOrderId = null; // handled once
                    await db.SaveChangesAsync(ct);
                }
                await FailAsync(subId, "Payment was not received in time.", ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Missed-payment handling failed for subscription {Id}", subId);
            }
        }
    }
}
