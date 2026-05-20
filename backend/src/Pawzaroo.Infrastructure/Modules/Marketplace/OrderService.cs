using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly IInventoryService _inventory;
    private readonly ICommissionConfigurationService _commission;
    private readonly INotificationService _notify;
    private readonly IPaymentGateway _paymentGateway;
    private readonly MarketplaceOptions _market;

    public OrderService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        IAuditLogger audit, IInventoryService inventory, ICommissionConfigurationService commission,
        INotificationService notify, IPaymentGateway paymentGateway, IOptions<MarketplaceOptions> market)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _inventory = inventory;
        _commission = commission;
        _notify = notify;
        _paymentGateway = paymentGateway;
        _market = market.Value;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    /// <summary>
    /// Cart -> Order. Snapshots prices, decrements stock atomically per line,
    /// resolves per-store commission, and ships an OrderPlaced Kafka event.
    /// Wrapped in a transaction so partial stock decrements never leak.
    /// </summary>
    public async Task<OrderDto> CheckoutAsync(CheckoutInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var cartLines = await _db.CartItems.Include(c => c.Product).ThenInclude(p => p.Store)
            .Where(c => c.UserId == uid).ToListAsync(ct);
        if (cartLines.Count == 0) throw new ValidationException(
            new Dictionary<string, string[]> { ["cart"] = new[] { "Cart is empty." } });

        // Bail out cleanly if the user picked a hosted-payment method we can't
        // actually initiate (no credentials, no public backend URL). Done before
        // the txn opens so we don't churn an order row that would only roll back.
        var paymentMethodNormalized = NormalizePaymentMethod(input.PaymentMethod);
        if (paymentMethodNormalized == "sslcommerz" && !_paymentGateway.IsConfigured)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["paymentMethod"] = new[]
                {
                    "Online payment is not configured on the server. " +
                    "Choose Cash on delivery, or ask the admin to set SslCommerz:StoreId, StorePassword, and BackendBaseUrl."
                }
            });

        string address;
        string? city, country;
        // SSLCommerz requires ship_state + ship_postcode for the initiate POST.
        // We capture them from the saved address book entry when available;
        // SslCommerzPaymentGateway falls back to safe defaults if both are null
        // so a one-off shipping address still goes through.
        string? state = null;
        string? postalCode = null;
        if (input.ShippingAddressId.HasValue)
        {
            var sa = await _db.ShippingAddresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == input.ShippingAddressId && a.UserId == uid, ct)
                ?? throw new NotFoundException("ShippingAddress", input.ShippingAddressId.Value);
            address = $"{sa.RecipientName}, {sa.PhoneNumber}, {sa.AddressLine1}" +
                      (string.IsNullOrEmpty(sa.AddressLine2) ? "" : $", {sa.AddressLine2}");
            city = sa.City; country = sa.Country;
            state = sa.State; postalCode = sa.PostalCode;
        }
        else
        {
            address = input.ShippingAddress!;
            city = input.ShippingCity;
            country = input.ShippingCountry;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var order = new Order
        {
            UserId = uid,
            OrderNumber = $"PZ{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
            ShippingAddress = address, ShippingCity = city, ShippingCountry = country,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Unpaid,
            ShipmentStatus = ShipmentStatus.NotShipped
        };

        var storeIds = new HashSet<Guid>();
        foreach (var ci in cartLines)
        {
            if (ci.Product.Store.ApprovalStatus != ApprovalStatus.Approved)
                throw new ConflictException($"Store for '{ci.Product.Name}' is not available.");

            var price = ci.Product.DiscountPrice ?? ci.Product.Price;
            var lineTotal = price * ci.Quantity;
            var commissionPct = await _commission.ResolveAsync(ci.Product.StoreId, ci.Product.CategoryId, DateTime.UtcNow, ct);
            var commission = Math.Round(lineTotal * commissionPct / 100m, 2, MidpointRounding.AwayFromZero);

            order.Items.Add(new OrderItem
            {
                ProductId = ci.ProductId,
                StoreId = ci.Product.StoreId,
                Quantity = ci.Quantity,
                UnitPrice = price,
                Total = lineTotal,
                CommissionAmount = commission
            });
            storeIds.Add(ci.Product.StoreId);
        }

        order.Subtotal = order.Items.Sum(i => i.Total);

        // Resolve coupon (if any) within the transaction so the redemption
        // counter increments atomically with the order being placed.
        decimal discount = 0m;
        if (!string.IsNullOrWhiteSpace(input.CouponCode))
        {
            var coupon = await _db.Coupons
                .FirstOrDefaultAsync(c => c.Code == input.CouponCode, ct);
            if (coupon is null) throw new ValidationException(new Dictionary<string, string[]> { ["couponCode"] = new[] { "Unknown coupon code." } });
            if (!coupon.IsActive) throw new ValidationException(new Dictionary<string, string[]> { ["couponCode"] = new[] { "Coupon is inactive." } });
            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < DateTime.UtcNow)
                throw new ValidationException(new Dictionary<string, string[]> { ["couponCode"] = new[] { "Coupon has expired." } });
            if (order.Subtotal < coupon.MinOrderAmount)
                throw new ValidationException(new Dictionary<string, string[]> { ["couponCode"] = new[] { $"Minimum order subtotal {coupon.MinOrderAmount:0.00} not met." } });
            if (coupon.MaxRedemptions.HasValue && coupon.RedemptionsCount >= coupon.MaxRedemptions.Value)
                throw new ValidationException(new Dictionary<string, string[]> { ["couponCode"] = new[] { "Coupon redemption limit reached." } });

            discount = coupon.Type == CouponType.Percent
                ? Math.Round(order.Subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero)
                : Math.Min(coupon.Value, order.Subtotal);

            order.CouponCode = coupon.Code;
            order.DiscountAmount = discount;
            coupon.RedemptionsCount += 1;
        }

        var subtotalAfterDiscount = order.Subtotal - discount;
        var (shipping, tax, total) = FeeCalculator.Compute(subtotalAfterDiscount, _market);
        order.ShippingFee = shipping;
        order.Tax = tax;
        order.Total = total;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Atomic stock decrement per line (throws ConflictException on oversell).
        foreach (var oi in order.Items)
            await _inventory.DecrementForOrderAsync(oi.ProductId, oi.Quantity, order.Id, ct);

        var payment = new Payment
        {
            OrderId = order.Id, Amount = order.Total,
            Method = paymentMethodNormalized,
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        _db.CartItems.RemoveRange(cartLines);
        await _db.SaveChangesAsync(ct);

        // Initiate the hosted-payment session BEFORE committing so that if the
        // gateway is unreachable the entire txn rolls back: no order row, no
        // inventory decrement, no orphaned Payment.
        string? checkoutUrl = null;
        if (paymentMethodNormalized == "sslcommerz")
        {
            var customer = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => new { u.Email, u.DisplayName, u.PhoneNumber })
                .FirstAsync(ct);

            var lineItems = order.Items
                .Select(i => new PaymentLineItem(
                    Name: cartLines.First(c => c.ProductId == i.ProductId).Product.Name,
                    UnitAmount: i.UnitPrice,
                    Quantity: i.Quantity))
                .ToList();

            var session = await _paymentGateway.CreateCheckoutSessionAsync(new PaymentCheckoutRequest(
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                Currency: "BDT",
                TotalAmount: order.Total,
                CustomerEmail: customer.Email,
                CustomerName: customer.DisplayName,
                CustomerPhone: customer.PhoneNumber,
                ShippingAddress: order.ShippingAddress,
                ShippingCity: order.ShippingCity,
                ShippingState: state,
                ShippingCountry: order.ShippingCountry,
                ShippingPostalCode: postalCode,
                LineItems: lineItems), ct);

            payment.TransactionRef = session.SessionId;
            checkoutUrl = session.CheckoutUrl;
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderPlaced(order.Id, order.OrderNumber, uid, order.Total, order.Items.Count,
                storeIds.ToList(), DateTime.UtcNow),
            order.Id.ToString(), ct);

        foreach (var sid in storeIds)
        {
            var ownerId = await _db.Stores.AsNoTracking().Where(s => s.Id == sid).Select(s => s.OwnerUserId).FirstOrDefaultAsync(ct);
            if (ownerId != Guid.Empty)
                await _notify.NotifyUserAsync(ownerId, "New order received",
                    $"Order {order.OrderNumber} placed.", new { orderId = order.Id }, ct);
        }

        await _audit.LogAsync("order.checkout", "Order", order.Id.ToString(), order.OrderNumber, ct: ct);
        var dto = (await GetByIdAsync(order.Id, ct))!;
        return checkoutUrl is null ? dto : dto with { PaymentCheckoutUrl = checkoutUrl };
    }

    private static string NormalizePaymentMethod(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "cod";
        var v = raw.Trim().ToLowerInvariant();
        return v switch
        {
            "sslcommerz" or "ssl" or "card" => "sslcommerz",
            "cod" or "cash" or "cash_on_delivery" => "cod",
            _ => v
        };
    }

    // EF Core projection through Order.User + Order.DeliveryAssignment.DeliveryUser
    // didn't translate reliably (Order/User/DeliveryAssignment all have a global
    // soft-delete query filter from ApplicationDbContext.OnModelCreating, and
    // chained nav projections with that combo can fail at runtime). We
    // materialize with Include and map in memory — bulletproof and the volumes
    // here don't justify a hand-tuned projection.
    private IQueryable<Order> OrderWithRelations() =>
        _db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Images)
            .Include(o => o.Items).ThenInclude(i => i.Store)
            .Include(o => o.DeliveryAssignment!).ThenInclude(a => a.DeliveryUser);

    public async Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var canModerate = _current.Permissions.Contains(Permissions.Orders.View);

        var order = await OrderWithRelations().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return null;

        // Buyer can read their own; store owners can read orders for their store; admin always.
        if (canModerate || order.UserId == uid) return ToDto(order);

        var hasMyStore = await _db.OrderItems.AsNoTracking()
            .AnyAsync(i => i.OrderId == orderId && i.Store.OwnerUserId == uid, ct);
        if (hasMyStore) return ToDto(order);

        throw new ForbiddenException();
    }

    public async Task<PageResult<OrderDto>> ListMineAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = OrderWithRelations().Where(o => o.UserId == uid);
        var total = await _db.Orders.AsNoTracking().Where(o => o.UserId == uid).CountAsync(ct);
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<OrderDto>(orders.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<PageResult<OrderDto>> ListForStoreAsync(Guid storeId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        var owns = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == storeId && s.OwnerUserId == uid, ct);
        if (!owns && !_current.Permissions.Contains(Permissions.Orders.View)) throw new ForbiddenException();

        var baseQ = _db.Orders.AsNoTracking().Where(o => o.Items.Any(i => i.StoreId == storeId));
        if (status.HasValue) baseQ = baseQ.Where(o => o.Status == status);
        var total = await baseQ.CountAsync(ct);

        var q = OrderWithRelations().Where(o => o.Items.Any(i => i.StoreId == storeId));
        if (status.HasValue) q = q.Where(o => o.Status == status);
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<OrderDto>(orders.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<PageResult<OrderDto>> ListForAdminAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.View)) throw new ForbiddenException();

        var baseQ = _db.Orders.AsNoTracking().AsQueryable();
        if (status.HasValue) baseQ = baseQ.Where(o => o.Status == status);
        var total = await baseQ.CountAsync(ct);

        var q = OrderWithRelations();
        if (status.HasValue) q = q.Where(o => o.Status == status);
        var orders = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<OrderDto>(orders.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task UpdateStatusAsync(Guid orderId, OrderStatus status, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new NotFoundException("Order", orderId);
        await EnsureStoreOwnerOrAdmin(order, ct);
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderStatusChanged(order.Id, order.OrderNumber, status.ToString(), DateTime.UtcNow),
            order.Id.ToString(), ct);
        await _notify.NotifyUserAsync(order.UserId, "Order updated",
            $"{order.OrderNumber} is now {status}", new { orderId = order.Id }, ct);
    }

    public async Task UpdateShipmentStatusAsync(Guid orderId, ShipmentStatus status, string? trackingNumber, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new NotFoundException("Order", orderId);
        await EnsureStoreOwnerOrAdmin(order, ct);
        order.ShipmentStatus = status;
        if (!string.IsNullOrEmpty(trackingNumber)) order.TrackingNumber = trackingNumber;
        if (status == ShipmentStatus.Delivered) order.Status = OrderStatus.Delivered;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderShipmentStatusChanged(order.Id, order.OrderNumber, status.ToString(), trackingNumber, DateTime.UtcNow),
            order.Id.ToString(), ct);
        await _notify.NotifyUserAsync(order.UserId, "Shipment update",
            $"{order.OrderNumber}: {status}", new { orderId = order.Id, trackingNumber }, ct);
    }

    public async Task CancelAsync(Guid orderId, string? reason, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        var uid = Uid();
        bool isAdmin = _current.Permissions.Contains(Permissions.Orders.Cancel);
        if (!isAdmin && order.UserId != uid) throw new ForbiddenException();
        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new ConflictException("Cannot cancel a shipped or delivered order.");

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = uid;

        // Restock — append-only adjustments + bump StockQuantity.
        foreach (var item in order.Items)
        {
            await _db.Products.Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity), ct);
            var qty = await _db.Products.AsNoTracking().Where(p => p.Id == item.ProductId).Select(p => (int?)p.StockQuantity).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Product", item.ProductId);
            _db.InventoryAdjustments.Add(new InventoryAdjustment
            {
                ProductId = item.ProductId, OrderId = order.Id,
                QuantityChange = item.Quantity, QuantityAfter = qty,
                Reason = InventoryReason.Return, Notes = reason
            });
        }
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderCancelled(order.Id, order.OrderNumber, uid, reason, DateTime.UtcNow), order.Id.ToString(), ct);
        await _audit.LogAsync("order.cancel", "Order", order.Id.ToString(), reason, ct: ct);
    }

    /// <summary>
    /// Store owner / admin denies an order (vs buyer Cancel). Restocks inventory
    /// like CancelAsync but sets Status=Denied so downstream consumers can tell
    /// the two apart for analytics & buyer-facing messaging.
    /// </summary>
    public async Task DenyAsync(Guid orderId, string? reason, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        await EnsureStoreOwnerOrAdmin(order, ct);
        if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new ConflictException("Cannot deny a shipped or delivered order.");
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Denied)
            throw new ConflictException("Order is already closed.");

        order.Status = OrderStatus.Denied;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = _current.UserId;

        foreach (var item in order.Items)
        {
            await _db.Products.Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity), ct);
            var qty = await _db.Products.AsNoTracking().Where(p => p.Id == item.ProductId).Select(p => (int?)p.StockQuantity).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Product", item.ProductId);
            _db.InventoryAdjustments.Add(new InventoryAdjustment
            {
                ProductId = item.ProductId, OrderId = order.Id,
                QuantityChange = item.Quantity, QuantityAfter = qty,
                Reason = InventoryReason.Return, Notes = reason
            });
        }
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderStatusChanged(order.Id, order.OrderNumber, OrderStatus.Denied.ToString(), DateTime.UtcNow),
            order.Id.ToString(), ct);
        await _notify.NotifyUserAsync(order.UserId, "Order denied",
            $"{order.OrderNumber} was denied. {reason}", new { orderId = order.Id, reason }, ct);
        await _audit.LogAsync("order.deny", "Order", order.Id.ToString(), reason, ct: ct);
    }

    public async Task RefundAsync(Guid orderId, decimal? amount, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.Refund)) throw new ForbiddenException();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new NotFoundException("Order", orderId);
        order.PaymentStatus = PaymentStatus.Refunded;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);

        var refundAmount = amount ?? order.Total;
        await _kafka.PublishAsync(MarketplaceTopics.PaymentEvents,
            new OrderRefunded(order.Id, order.OrderNumber, refundAmount, DateTime.UtcNow), order.Id.ToString(), ct);
        await _audit.LogAsync("order.refund", "Order", order.Id.ToString(), refundAmount.ToString("0.00"), ct: ct);
    }

    /// <summary>
    /// Idempotent: if the order is already Paid we just stamp the provider ref
    /// (a duplicate IPN). We deliberately don't trust the gateway's amount as
    /// the source of truth — we compare against order.Total and refuse to flip
    /// to Paid on mismatch, leaving the order Pending for manual review.
    /// </summary>
    public async Task MarkPaymentSucceededAsync(Guid orderId, string providerRef, decimal? amountValidated, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        if (payment is not null)
        {
            payment.TransactionRef = providerRef;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            await _db.SaveChangesAsync(ct);
            return;
        }

        if (amountValidated.HasValue && Math.Abs(amountValidated.Value - order.Total) > 0.01m)
        {
            await _audit.LogAsync("order.payment.mismatch", "Order", order.Id.ToString(),
                $"expected={order.Total} got={amountValidated}", ct: ct);
            await _db.SaveChangesAsync(ct);
            return;
        }

        order.PaymentStatus = PaymentStatus.Paid;
        if (order.Status == OrderStatus.Created) order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        if (payment is not null) payment.Status = PaymentStatus.Paid;

        await _db.SaveChangesAsync(ct);

        if (payment is not null)
            await _kafka.PublishAsync(MarketplaceTopics.PaymentEvents,
                new PaymentSucceeded(order.Id, payment.Id, order.Total, providerRef, DateTime.UtcNow),
                order.Id.ToString(), ct);

        await _notify.NotifyUserAsync(order.UserId, "Payment received",
            $"{order.OrderNumber} is confirmed.", new { orderId = order.Id }, ct);

        await _audit.LogAsync("order.payment.succeeded", "Order", order.Id.ToString(), providerRef, ct: ct);
    }

    /// <summary>
    /// Marks payment failed/cancelled. We do NOT auto-cancel the order — the
    /// user may retry payment from the cancel page. CancelAsync (with restock)
    /// is a separate, explicit action.
    /// </summary>
    public async Task MarkPaymentFailedAsync(Guid orderId, string providerRef, bool cancelled, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        if (order.PaymentStatus == PaymentStatus.Paid) return; // late callback after success — ignore

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Failed;
            payment.TransactionRef = providerRef;
            payment.UpdatedAt = DateTime.UtcNow;
        }
        order.PaymentStatus = PaymentStatus.Failed;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (payment is not null)
            await _kafka.PublishAsync(MarketplaceTopics.PaymentEvents,
                new PaymentFailed(order.Id, payment.Id, cancelled ? "cancelled" : "failed", DateTime.UtcNow),
                order.Id.ToString(), ct);

        await _audit.LogAsync(
            cancelled ? "order.payment.cancelled" : "order.payment.failed",
            "Order", order.Id.ToString(), providerRef, ct: ct);
    }

    // Authorization for order writes (status / shipment updates).
    //
    // Trust model:
    //  - "Admin" path requires Orders.Cancel (write-level), NOT Orders.View. View
    //    is a read-only permission and was previously accepted here, which let
    //    any read-only support role mutate orders.
    //  - "Store owner" path joins through OrderItems.Store.OwnerUserId: a store
    //    owner with at least one line-item in the order can update the order's
    //    shared status. Multi-store orders share a single status by design — if
    //    that ever changes, this check must become per-store.
    private async Task EnsureStoreOwnerOrAdmin(Order order, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (_current.Permissions.Contains(Permissions.Orders.Cancel)) return;
        var owns = await _db.OrderItems.AsNoTracking()
            .AnyAsync(i => i.OrderId == order.Id && i.Store.OwnerUserId == uid, ct);
        if (!owns) throw new ForbiddenException();
    }

    // In-memory mapper. Assumes the Order entity was loaded with the navs we
    // touch (Items.Product.Images, Items.Store, User, optionally
    // DeliveryAssignment.DeliveryUser). OrderWithRelations() takes care of that.
    private static OrderDto ToDto(Order o) => new OrderDto(
        Id: o.Id, OrderNumber: o.OrderNumber, UserId: o.UserId,
        Subtotal: o.Subtotal, ShippingFee: o.ShippingFee, Tax: o.Tax, Total: o.Total,
        Status: o.Status, PaymentStatus: o.PaymentStatus, ShipmentStatus: o.ShipmentStatus,
        ShippingAddress: o.ShippingAddress, ShippingCity: o.ShippingCity, ShippingCountry: o.ShippingCountry,
        TrackingNumber: o.TrackingNumber,
        Items: o.Items.Select(i => new OrderItemDto(
            i.Id, i.ProductId, i.Product?.Name ?? string.Empty,
            i.Product?.Images?.OrderBy(x => x.OrderIndex).Select(x => x.Url).FirstOrDefault(),
            i.StoreId, i.Store?.Name ?? string.Empty,
            i.Quantity, i.UnitPrice, i.Total, i.CommissionAmount)).ToList(),
        CreatedAt: o.CreatedAt,
        DiscountAmount: o.DiscountAmount,
        CouponCode: o.CouponCode,
        CustomerName: o.User?.DisplayName,
        CustomerEmail: o.User?.Email,
        CustomerPhone: o.User?.PhoneNumber,
        DeliveryAssignmentId: o.DeliveryAssignment?.Id,
        DeliveryUserId: o.DeliveryAssignment?.DeliveryUserId,
        DeliveryUserName: o.DeliveryAssignment?.DeliveryUser?.DisplayName,
        DeliveryStatus: o.DeliveryAssignment?.Status);
}
