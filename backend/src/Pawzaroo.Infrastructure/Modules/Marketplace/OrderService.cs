using Microsoft.EntityFrameworkCore;
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

    public OrderService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        IAuditLogger audit, IInventoryService inventory, ICommissionConfigurationService commission,
        INotificationService notify, IPaymentGateway paymentGateway)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _inventory = inventory;
        _commission = commission;
        _notify = notify;
        _paymentGateway = paymentGateway;
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

        string address;
        string? city, country;
        if (input.ShippingAddressId.HasValue)
        {
            var sa = await _db.ShippingAddresses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == input.ShippingAddressId && a.UserId == uid, ct)
                ?? throw new NotFoundException("ShippingAddress", input.ShippingAddressId.Value);
            address = $"{sa.RecipientName}, {sa.PhoneNumber}, {sa.AddressLine1}" +
                      (string.IsNullOrEmpty(sa.AddressLine2) ? "" : $", {sa.AddressLine2}");
            city = sa.City; country = sa.Country;
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
        order.ShippingFee = 0m; // pluggable
        order.Tax = 0m;
        order.Total = order.Subtotal + order.ShippingFee + order.Tax;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Atomic stock decrement per line (throws ConflictException on oversell).
        foreach (var oi in order.Items)
            await _inventory.DecrementForOrderAsync(oi.ProductId, oi.Quantity, order.Id, ct);

        var paymentMethod = NormalizePaymentMethod(input.PaymentMethod);
        var payment = new Payment
        {
            OrderId = order.Id, Amount = order.Total,
            Method = paymentMethod,
            Status = PaymentStatus.Pending
        };
        _db.Payments.Add(payment);
        _db.CartItems.RemoveRange(cartLines);
        await _db.SaveChangesAsync(ct);

        // Initiate the hosted-payment session BEFORE committing so that if the
        // gateway is unreachable the entire txn rolls back: no order row, no
        // inventory decrement, no orphaned Payment.
        string? checkoutUrl = null;
        if (paymentMethod == "sslcommerz")
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
                ShippingCountry: order.ShippingCountry,
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

    public async Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var canModerate = _current.Permissions.Contains(Permissions.Orders.View);

        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => ToDto(o))
            .FirstOrDefaultAsync(ct);
        if (order is null) return null;

        // Buyer can read their own; store owners can read orders for their store; admin always.
        if (canModerate || order.UserId == uid) return order;

        var hasMyStore = await _db.OrderItems.AsNoTracking()
            .AnyAsync(i => i.OrderId == orderId && i.Store.OwnerUserId == uid, ct);
        if (hasMyStore) return order;

        throw new ForbiddenException();
    }

    public async Task<PageResult<OrderDto>> ListMineAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = _db.Orders.AsNoTracking().Where(o => o.UserId == uid);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => ToDto(o)).ToListAsync(ct);
        return new PageResult<OrderDto>(items, total, page, pageSize);
    }

    public async Task<PageResult<OrderDto>> ListForStoreAsync(Guid storeId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        var owns = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == storeId && s.OwnerUserId == uid, ct);
        if (!owns && !_current.Permissions.Contains(Permissions.Orders.View)) throw new ForbiddenException();

        var q = _db.Orders.AsNoTracking()
            .Where(o => o.Items.Any(i => i.StoreId == storeId));
        if (status.HasValue) q = q.Where(o => o.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => ToDto(o)).ToListAsync(ct);
        return new PageResult<OrderDto>(items, total, page, pageSize);
    }

    public async Task<PageResult<OrderDto>> ListForAdminAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.View)) throw new ForbiddenException();
        var q = _db.Orders.AsNoTracking();
        if (status.HasValue) q = q.Where(o => o.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => ToDto(o)).ToListAsync(ct);
        return new PageResult<OrderDto>(items, total, page, pageSize);
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

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.OrderNumber, o.UserId,
        o.Subtotal, o.ShippingFee, o.Tax, o.Total,
        o.Status, o.PaymentStatus, o.ShipmentStatus,
        o.ShippingAddress, o.ShippingCity, o.ShippingCountry,
        o.TrackingNumber,
        o.Items.Select(i => new OrderItemDto(
            i.Id, i.ProductId, i.Product.Name,
            i.Product.Images.OrderBy(x => x.OrderIndex).Select(x => x.Url).FirstOrDefault(),
            i.StoreId, i.Store.Name,
            i.Quantity, i.UnitPrice, i.Total, i.CommissionAmount)).ToList(),
        o.CreatedAt);
}
