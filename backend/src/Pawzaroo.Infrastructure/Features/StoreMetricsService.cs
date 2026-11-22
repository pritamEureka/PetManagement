using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Seller scorecard for the signed-in store owner.
///  - cancellation rate: cancelled/denied orders / all orders in the window
///  - late-shipment rate: orders with a Ship SlaBreach / non-cancelled orders
///  - return rate: return requests / delivered order lines
///  - dispute rate: there is no Disputed status, so this is the share of return requests the admin REJECTED (contested returns)
///  - conversion: distinct buyers / distinct viewers (RecentlyViewedItem of the store's products); unavailable without view data
/// </summary>
public class StoreMetricsService : IStoreMetricsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public StoreMetricsService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    public async Task<StoreMetricsDto> GetMineAsync(int days, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        days = Math.Clamp(days <= 0 ? 30 : days, 1, 365);
        var storeId = await _db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Store", uid);
        var since = DateTime.UtcNow.AddDays(-days);

        var lines = _db.OrderItems.AsNoTracking().Where(i => i.StoreId == storeId && i.Order.CreatedAt >= since);
        var cancelledStatuses = new[] { OrderStatus.Cancelled, OrderStatus.Denied };

        var orders = await lines.Select(i => i.OrderId).Distinct().CountAsync(ct);
        var cancelled = await lines.Where(i => cancelledStatuses.Contains(i.Order.Status)).Select(i => i.OrderId).Distinct().CountAsync(ct);
        var live = lines.Where(i => !cancelledStatuses.Contains(i.Order.Status));
        var liveOrders = orders - cancelled;
        var revenue = await live.SumAsync(i => (decimal?)i.Total, ct) ?? 0m;

        var lateShip = await _db.Set<SlaBreach>().AsNoTracking()
            .Where(b => b.StoreId == storeId && b.Kind == SlaBreachKind.Ship && b.BreachedAt >= since)
            .Select(b => b.OrderId).Distinct().CountAsync(ct);

        var deliveredLines = await lines.CountAsync(i => i.Order.Status == OrderStatus.Delivered, ct);
        var returns = _db.ReturnRequests.AsNoTracking().Where(r => r.OrderItem.StoreId == storeId && r.CreatedAt >= since);
        var returnCount = await returns.CountAsync(ct);
        var rejected = await returns.CountAsync(r => r.Status == ApprovalStatus.Rejected, ct);

        var viewers = await _db.Set<RecentlyViewedItem>().AsNoTracking()
            .Where(v => v.ViewedAt >= since && _db.Products.Any(p => p.Id == v.ProductId && p.StoreId == storeId))
            .Select(v => v.UserId).Distinct().CountAsync(ct);
        var buyers = await live.Select(i => i.Order.UserId).Distinct().CountAsync(ct);

        var ratings = _db.Set<StoreReview>().AsNoTracking().Where(r => r.StoreId == storeId);
        var reviewCount = await ratings.CountAsync(ct);
        var avg = reviewCount == 0 ? 0m : Math.Round((decimal)(await ratings.AverageAsync(r => (double)r.Rating, ct)), 2);

        var daily = (await live.GroupBy(i => i.Order.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Sum = g.Sum(x => x.Total) }).OrderBy(x => x.Day).ToListAsync(ct))
            .Select(x => new MetricPointDto(x.Day.ToString("MM-dd"), x.Sum)).ToList();

        // Order/limit on an anonymous projection (EF cannot translate OrderBy over a DTO constructor call), then map.
        var topRows = await live.GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Total) })
            .OrderByDescending(t => t.Revenue).Take(5).ToListAsync(ct);
        var top = topRows.Select(t => new TopProductMetricDto(t.ProductId, t.Name, t.Quantity, t.Revenue)).ToList();

        return new StoreMetricsDto(storeId, days, orders, cancelled, MetricsMath.Rate(cancelled, orders),
            liveOrders, lateShip, MetricsMath.Rate(lateShip, liveOrders),
            returnCount, MetricsMath.Rate(returnCount, deliveredLines),
            rejected, MetricsMath.Rate(rejected, returnCount),
            viewers, MetricsMath.Rate(buyers, viewers), viewers > 0,
            avg, reviewCount, revenue, daily, top);
    }
}
