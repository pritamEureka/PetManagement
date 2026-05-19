using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// One-stop dashboard endpoints. All numbers are computed in a single round-trip
/// per group so the SPA needs at most three calls (`overview`, `series`, `top`).
///
/// Reads require <c>users.view</c>. Each widget's underlying detail page has its
/// own finer permission (e.g. /admin/orders → orders.view).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize]
public class AdminDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public AdminDashboardController(ApplicationDbContext db) => _db = db;

    public record ShipmentBreakdown(
        long NotShipped, long Processing, long InTransit,
        long OutForDelivery, long Delivered, long Failed);

    public record OverviewDto(
        long TotalUsers, long ActiveUsers, long NewUsersToday,
        long PendingDoctorApprovals, long PendingStoreApprovals, long PendingAdoptionListings,
        long NewFeedPostsToday, long OpenReports,
        long TotalAppointments, long AppointmentsToday,
        long TotalOrders, long PendingOrders, decimal TotalRevenue, decimal CommissionEarned,
        long ActiveStores, long ActiveDoctors,
        long TotalProducts, long ActiveProducts, long LowStockProducts,
        ShipmentBreakdown ShipmentStatusBreakdown);

    [HttpGet("overview")]
    [Permission(Permissions.Users.View)]
    public async Task<ActionResult<OverviewDto>> Overview(CancellationToken ct)
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Independent counts run in parallel — much faster than serial awaits.
        var users        = _db.Users.AsNoTracking();
        var stores       = _db.Stores.AsNoTracking();
        var doctors      = _db.Doctors.AsNoTracking();
        var listings     = _db.AdoptionListings.AsNoTracking();
        var posts        = _db.Posts.AsNoTracking();
        var reports      = _db.ContentReports.AsNoTracking();
        var appointments = _db.Appointments.AsNoTracking();
        var orders       = _db.Orders.AsNoTracking();
        var items        = _db.OrderItems.AsNoTracking();

        var totalUsersT       = users.LongCountAsync(ct);
        var activeUsersT      = users.LongCountAsync(u => u.IsActive && !u.IsSuspended, ct);
        var newUsersTodayT    = users.LongCountAsync(u => u.CreatedAt >= today, ct);
        var pendingVetsT      = doctors.LongCountAsync(d => d.ApprovalStatus == ApprovalStatus.Pending, ct);
        var pendingStoresT    = stores.LongCountAsync(s => s.ApprovalStatus == ApprovalStatus.Pending, ct);
        var pendingListingsT  = listings.LongCountAsync(l => l.Status == AdoptionListingStatus.PendingApproval, ct);
        var newPostsTodayT    = posts.LongCountAsync(p => p.CreatedAt >= today, ct);
        var openReportsT      = reports.LongCountAsync(r => r.Status == ReportStatus.Open, ct);
        var totalAptT         = appointments.LongCountAsync(ct);
        var aptTodayT         = appointments.LongCountAsync(a => a.ScheduledAt >= today && a.ScheduledAt < tomorrow, ct);
        var totalOrdersT      = orders.LongCountAsync(ct);
        var pendingOrdersT    = orders.LongCountAsync(o => o.Status == OrderStatus.Created || o.Status == OrderStatus.Confirmed, ct);
        var revenueT          = orders.Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Denied)
                                       .SumAsync(o => (decimal?)o.Total, ct);
        var commissionT       = items.SumAsync(i => (decimal?)i.CommissionAmount, ct);
        var activeStoresT     = stores.LongCountAsync(s => s.ApprovalStatus == ApprovalStatus.Approved, ct);
        var activeDoctorsT    = doctors.LongCountAsync(d => d.ApprovalStatus == ApprovalStatus.Approved, ct);

        var products          = _db.Products.AsNoTracking();
        var totalProductsT    = products.LongCountAsync(ct);
        var activeProductsT   = products.LongCountAsync(p => p.IsActive, ct);
        var lowStockT         = products.LongCountAsync(p => p.IsActive && p.StockQuantity <= 5, ct);

        // Shipment breakdown — six independent counts derived from the same orders set.
        var shipNotShippedT  = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.NotShipped, ct);
        var shipProcessingT  = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.Processing, ct);
        var shipInTransitT   = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.InTransit, ct);
        var shipOutForDelT   = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.OutForDelivery, ct);
        var shipDeliveredT   = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.Delivered, ct);
        var shipFailedT      = orders.LongCountAsync(o => o.ShipmentStatus == ShipmentStatus.Failed, ct);

        await Task.WhenAll(
            totalUsersT, activeUsersT, newUsersTodayT,
            pendingVetsT, pendingStoresT, pendingListingsT,
            newPostsTodayT, openReportsT,
            totalAptT, aptTodayT,
            totalOrdersT, pendingOrdersT, revenueT, commissionT,
            activeStoresT, activeDoctorsT,
            totalProductsT, activeProductsT, lowStockT,
            shipNotShippedT, shipProcessingT, shipInTransitT,
            shipOutForDelT, shipDeliveredT, shipFailedT);

        return new OverviewDto(
            TotalUsers: totalUsersT.Result,
            ActiveUsers: activeUsersT.Result,
            NewUsersToday: newUsersTodayT.Result,
            PendingDoctorApprovals: pendingVetsT.Result,
            PendingStoreApprovals: pendingStoresT.Result,
            PendingAdoptionListings: pendingListingsT.Result,
            NewFeedPostsToday: newPostsTodayT.Result,
            OpenReports: openReportsT.Result,
            TotalAppointments: totalAptT.Result,
            AppointmentsToday: aptTodayT.Result,
            TotalOrders: totalOrdersT.Result,
            PendingOrders: pendingOrdersT.Result,
            TotalRevenue: revenueT.Result ?? 0m,
            CommissionEarned: commissionT.Result ?? 0m,
            ActiveStores: activeStoresT.Result,
            ActiveDoctors: activeDoctorsT.Result,
            TotalProducts: totalProductsT.Result,
            ActiveProducts: activeProductsT.Result,
            LowStockProducts: lowStockT.Result,
            ShipmentStatusBreakdown: new ShipmentBreakdown(
                shipNotShippedT.Result, shipProcessingT.Result, shipInTransitT.Result,
                shipOutForDelT.Result, shipDeliveredT.Result, shipFailedT.Result));
    }

    public record DailyPoint(string Date, long Users, long Orders, decimal Revenue);

    /// <summary>Last N days of users / orders / revenue — feeds the dashboard line chart.</summary>
    [HttpGet("series")]
    [Permission(Permissions.Users.View)]
    public async Task<IReadOnlyList<DailyPoint>> Series([FromQuery] int days = 30, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 7, 90);
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var userRows = await _db.Users.AsNoTracking()
            .Where(u => u.CreatedAt >= from)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = (long)g.Count() })
            .ToListAsync(ct);

        var orderRows = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = (long)g.Count(), Revenue = g.Sum(o => o.Total) })
            .ToListAsync(ct);

        var byDate = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(d =>
            {
                var u = userRows.FirstOrDefault(x => x.Date == d);
                var o = orderRows.FirstOrDefault(x => x.Date == d);
                return new DailyPoint(d.ToString("yyyy-MM-dd"),
                    u?.Count ?? 0, o?.Count ?? 0, o?.Revenue ?? 0m);
            })
            .ToList();

        return byDate;
    }

    public record TopAnimal(string Type, long Listings, long Adopted);
    public record TopProduct(Guid ProductId, string Name, long UnitsSold, decimal Revenue);

    [HttpGet("top")]
    [Permission(Permissions.Users.View)]
    public async Task<IActionResult> Top([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 7, 90));

        var animals = await _db.AdoptionListings.AsNoTracking()
            .Where(l => l.CreatedAt >= from)
            .GroupBy(l => l.AnimalType)
            .Select(g => new TopAnimal(
                g.Key.ToString(),
                g.LongCount(),
                g.LongCount(x => x.Status == AdoptionListingStatus.Adopted)))
            .OrderByDescending(x => x.Listings)
            .Take(8)
            .ToListAsync(ct);

        var products = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Order.CreatedAt >= from && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => new { i.ProductId, i.Product.Name })
            .Select(g => new TopProduct(
                g.Key.ProductId, g.Key.Name,
                g.Sum(x => (long)x.Quantity),
                g.Sum(x => x.Total)))
            .OrderByDescending(x => x.Revenue)
            .Take(8)
            .ToListAsync(ct);

        return Ok(new { animals, products });
    }
}
