using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Item level (partial) refunds and the per-store split view of a multi-store order.
/// An order holds items of several stores; the discount, shipping and tax are spread pro-rata
/// (LedgerMath.AllocateProRata) so the per-store numbers always add up to the order.
/// Design notes:
///  - PaymentStatus is NOT changed (no PartiallyRefunded enum member); the refunded amount is derived from OrderRefund rows.
///  - One partial refund per order item (unique index) because ledger refund/reversal entries are once per item.
///  - IPaymentGateway has no refund API: rows are stored as GatewayStatus = "RefundInitiated" for manual/gateway follow-up.
/// </summary>
public class PartialRefundService : IPartialRefundService
{
    private const string VariantNotePrefix = "variant:";   // same tag OrderService writes on Sale adjustments

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ILedgerRecorder _ledger;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;

    public PartialRefundService(ApplicationDbContext db, ICurrentUserService current, ILedgerRecorder ledger,
        IKafkaProducer kafka, IAuditLogger audit, INotificationService notify)
    { _db = db; _current = current; _ledger = ledger; _kafka = kafka; _audit = audit; _notify = notify; }

    private static ValidationException Invalid(string f, string m) => new(new Dictionary<string, string[]> { [f] = new[] { m } });

    public async Task<PartialRefundResultDto> RefundItemsAsync(Guid orderId, IReadOnlyList<PartialRefundItemInput> items, string reason, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Orders.Refund)) throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(reason)) throw Invalid("reason", "A reason is required.");
        if (items is null || items.Count == 0) throw Invalid("items", "Select at least one item.");
        if (items.Select(i => i.OrderItemId).Distinct().Count() != items.Count) throw Invalid("items", "An item can only appear once.");
        if (items.Any(i => i.Quantity <= 0)) throw Invalid("items", "Quantities must be positive.");

        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        if (order.PaymentStatus != PaymentStatus.Paid)
            throw new ConflictException(order.PaymentStatus == PaymentStatus.Refunded
                ? "This order was already fully refunded."
                : "Only paid orders can be refunded.");

        var lineList = order.Items.OrderBy(i => i.Id).ToList();     // stable order for pro-rata allocation
        // Customer credits (loyalty points / gift cards) only reduce the payable total, so refund the cash actually paid.
        var credits = await _db.Set<OrderAppliedAdjustment>().AsNoTracking()
            .Where(a => a.OrderId == order.Id && a.ReversedAt == null && (a.Kind == "loyalty" || a.Kind == "giftcard"))
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        var nets = LedgerMath.NetOfDiscountAndCredits(lineList.Select(l => l.Total).ToList(), order.DiscountAmount, credits);

        var itemIds = items.Select(i => i.OrderItemId).ToList();
        var already = await _db.Set<OrderRefund>().AsNoTracking().Where(r => itemIds.Contains(r.OrderItemId))
            .Select(r => r.OrderItemId).ToListAsync(ct);
        var returned = await _db.ReturnRequests.AsNoTracking()
            .Where(r => itemIds.Contains(r.OrderItemId) && r.Status == ApprovalStatus.Approved)
            .Select(r => r.OrderItemId).ToListAsync(ct);

        var lines = new List<(OrderItem Item, int Qty, decimal Amount)>();
        foreach (var req in items)
        {
            var idx = lineList.FindIndex(l => l.Id == req.OrderItemId);
            if (idx < 0) throw Invalid("items", "An item does not belong to this order.");
            var item = lineList[idx];
            if (already.Contains(item.Id)) throw new ConflictException("An item has already been partially refunded.");
            if (returned.Contains(item.Id)) throw new ConflictException("An item was already refunded through an approved return.");
            if (req.Quantity > item.Quantity) throw Invalid("items", "Quantity exceeds the purchased quantity.");
            var amount = PartialRefundMath.LineRefund(nets[idx], item.Quantity, 0, 0m, req.Quantity);
            if (amount <= 0m) throw Invalid("items", "Nothing to refund for an item.");
            lines.Add((item, req.Quantity, amount));
        }

        var total = lines.Sum(l => l.Amount);
        var uid = _current.UserId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var (item, qty, amount) in lines)
            {
                _db.Set<OrderRefund>().Add(new OrderRefund
                {
                    OrderId = order.Id, OrderItemId = item.Id, Quantity = qty, Amount = amount, Reason = reason.Trim(),
                    GatewayStatus = "RefundInitiated",
                    Note = "No gateway refund API: refund must be completed with the payment provider.",
                    ByUserId = uid
                });
                await RestockAsync(order.Id, item, qty, ct);
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            throw new ConflictException("An item has already been partially refunded.");
        }

        // Ledger (idempotent per item, never throws): store earning refunded, commission reversed pro-rata.
        foreach (var (item, _, amount) in lines) await _ledger.RecordItemRefundAsync(item.Id, amount, ct);

        var refundedTotal = await _db.Set<OrderRefund>().AsNoTracking().Where(r => r.OrderId == order.Id).SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        await _kafka.PublishAsync(MarketplaceTopics.PaymentEvents,
            new OrderRefunded(order.Id, order.OrderNumber, total, DateTime.UtcNow), order.Id.ToString(), ct);
        try
        {
            await _notify.NotifyUserAsync(order.UserId, "Partial refund initiated",
                $"A refund of {total:0.00} for order {order.OrderNumber} was initiated.", new { orderId = order.Id, amount = total }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ }
        await _audit.LogAsync("order.refund.partial", "Order", order.Id.ToString(), $"{total:0.00}: {reason.Trim()}", ct: ct);

        return new PartialRefundResultDto(order.Id, total, "RefundInitiated",
            lines.Select(l => new PartialRefundLineDto(l.Item.Id, l.Qty, l.Amount)).ToList(), refundedTotal);
    }

    private async Task RestockAsync(Guid orderId, OrderItem item, int qty, CancellationToken ct)
    {
        var newQty = await _db.Products.Where(p => p.Id == item.ProductId).Select(p => p.StockQuantity).FirstOrDefaultAsync(ct) + qty;
        await _db.Products.Where(p => p.Id == item.ProductId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + qty), ct);
        _db.InventoryAdjustments.Add(new InventoryAdjustment
        {
            ProductId = item.ProductId, OrderId = orderId, QuantityChange = qty, QuantityAfter = newQty,
            Reason = InventoryReason.Return, Notes = "partial refund", PerformedById = _current.UserId
        });

        // Variant lines: the Sale adjustment of this order/product is tagged "variant:<id>"; mirror stock on that variant.
        var tag = await _db.InventoryAdjustments.AsNoTracking()
            .Where(a => a.OrderId == orderId && a.ProductId == item.ProductId && a.Reason == InventoryReason.Sale
                        && a.Notes != null && a.Notes.StartsWith(VariantNotePrefix))
            .Select(a => a.Notes).FirstOrDefaultAsync(ct);
        if (tag is not null && Guid.TryParse(tag.AsSpan(VariantNotePrefix.Length), out var vid))
            await _db.Set<ProductVariant>().Where(v => v.Id == vid)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + qty), ct);
    }

    private async Task<Order> LoadVisibleOrderAsync(Guid orderId, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        if (order.UserId == uid || _current.Permissions.Contains(Permissions.Orders.View)) return order;
        var storeIds = order.Items.Select(i => i.StoreId).Distinct().ToList();
        if (await _db.Stores.AsNoTracking().AnyAsync(s => storeIds.Contains(s.Id) && s.OwnerUserId == uid, ct)) return order;
        throw new ForbiddenException();
    }

    public async Task<OrderRefundSummaryDto> GetSummaryAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadVisibleOrderAsync(orderId, ct);
        var rows = await _db.Set<OrderRefund>().AsNoTracking().Where(r => r.OrderId == orderId).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        return new OrderRefundSummaryDto(orderId, order.Total, rows.Sum(r => r.Amount),
            rows.Select(r => new OrderRefundDto(r.Id, r.OrderItemId, r.Quantity, r.Amount, r.Reason, r.GatewayStatus, r.CreatedAt, r.ByUserId)).ToList());
    }

    public async Task<IReadOnlyList<StoreOrderSplitDto>> GetStoreSplitAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await LoadVisibleOrderAsync(orderId, ct);
        var items = order.Items.OrderBy(i => i.Id).ToList();
        var refunds = await _db.Set<OrderRefund>().AsNoTracking().Where(r => r.OrderId == orderId).ToListAsync(ct);
        var storeIds = items.Select(i => i.StoreId).Distinct().ToList();
        var names = await _db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return Split(order, items, refunds.GroupBy(r => r.OrderItemId).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)), names);
    }

    /// <summary>Pure split of an order into per-store figures (also used by tests).</summary>
    public static IReadOnlyList<StoreOrderSplitDto> Split(Order order, IReadOnlyList<OrderItem> items,
        IReadOnlyDictionary<Guid, decimal> refundedByItem, IReadOnlyDictionary<Guid, string> storeNames)
    {
        var discounts = LedgerMath.AllocateProRata(items.Select(i => i.Total).ToList(), order.DiscountAmount);
        var groups = items.Select((it, idx) => (Item: it, Discount: discounts[idx])).GroupBy(x => x.Item.StoreId).ToList();

        var gross = groups.Select(g => g.Sum(x => x.Item.Total)).ToList();
        var discount = groups.Select(g => g.Sum(x => x.Discount)).ToList();
        var net = gross.Select((gr, i) => gr - discount[i]).ToList();
        var shipping = LedgerMath.AllocateProRata(net, order.ShippingFee);
        var tax = LedgerMath.AllocateProRata(net, order.Tax);

        var result = new List<StoreOrderSplitDto>();
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var commission = g.Sum(x => x.Item.CommissionAmount);
            var refunded = g.Sum(x => refundedByItem.GetValueOrDefault(x.Item.Id));
            var total = net[i] + shipping[i] + tax[i];
            result.Add(new StoreOrderSplitDto(g.Key, storeNames.GetValueOrDefault(g.Key), gross[i], discount[i], net[i],
                shipping[i], tax[i], commission, total, refunded, net[i] - commission - refunded));
        }
        return result;
    }
}
