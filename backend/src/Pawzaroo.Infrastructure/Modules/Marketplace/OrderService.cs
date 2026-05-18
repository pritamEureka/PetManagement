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

    public OrderService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka,
        IAuditLogger audit, IInventoryService inventory, ICommissionConfigurationService commission,
        INotificationService notify)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
        _inventory = inventory;
        _commission = commission;
        _notify = notify;
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

        _db.Payments.Add(new Payment
        {
            OrderId = order.Id, Amount = order.Total,
            Method = input.PaymentMethod ?? "placeholder",
            Status = PaymentStatus.Pending
        });
        _db.CartItems.RemoveRange(cartLines);
        await _db.SaveChangesAsync(ct);

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
        return (await GetByIdAsync(order.Id, ct))!;
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
            var qty = await _db.Products.AsNoTracking().Where(p => p.Id == item.ProductId).Select(p => p.StockQuantity).FirstAsync(ct);
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

    private async Task EnsureStoreOwnerOrAdmin(Order order, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (_current.Permissions.Contains(Permissions.Orders.View)) return;
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
