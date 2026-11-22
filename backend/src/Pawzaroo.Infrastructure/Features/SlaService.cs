using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>SLA configuration, per-order SLA status and breach listings.</summary>
public class SlaService : ISlaService
{
    public const int DefaultMaxHoursToShip = 48;
    public const int DefaultMaxHoursToDeliver = 168;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditLogger _audit;

    public SlaService(ApplicationDbContext db, ICurrentUserService current, IAuditLogger audit)
    { _db = db; _current = current; _audit = audit; }

    private void Need(string perm) { if (!_current.Permissions.Contains(perm)) throw new ForbiddenException(); }

    private static FulfillmentSlaDto Map(FulfillmentSla s) =>
        new(s.Id, s.StoreId, s.MaxHoursToShip, s.MaxHoursToDeliver, s.PenaltyNote, s.IsActive);

    private static ValidationException Invalid(string f, string m) => new(new Dictionary<string, string[]> { [f] = new[] { m } });

    public async Task<IReadOnlyList<FulfillmentSlaDto>> ListConfigAsync(CancellationToken ct = default)
    {
        Need(Permissions.Settings.View);
        var rows = await _db.Set<FulfillmentSla>().AsNoTracking().OrderBy(s => s.StoreId).ToListAsync(ct);
        var list = rows.Select(Map).ToList();
        // Always show the effective platform default, even if none is stored yet.
        if (!list.Any(s => s.StoreId == null))
            list.Insert(0, new FulfillmentSlaDto(null, null, DefaultMaxHoursToShip, DefaultMaxHoursToDeliver, null, true));
        return list;
    }

    public async Task<FulfillmentSlaDto> UpsertConfigAsync(Guid? id, UpsertFulfillmentSlaInput input, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        if (input.MaxHoursToShip is < 1 or > 24 * 60) throw Invalid("maxHoursToShip", "Must be between 1 and 1440 hours.");
        if (input.MaxHoursToDeliver < input.MaxHoursToShip || input.MaxHoursToDeliver > 24 * 120)
            throw Invalid("maxHoursToDeliver", "Delivery target must be at least the ship target.");
        if (input.StoreId is { } sid && !await _db.Stores.AnyAsync(s => s.Id == sid, ct)) throw new NotFoundException("Store", sid);

        FulfillmentSla? s = null;
        if (id is { } rid) s = await _db.Set<FulfillmentSla>().FirstOrDefaultAsync(x => x.Id == rid, ct) ?? throw new NotFoundException("FulfillmentSla", rid);
        // One config per scope (store or platform): editing by scope when no id was passed.
        s ??= await _db.Set<FulfillmentSla>().FirstOrDefaultAsync(x => x.StoreId == input.StoreId, ct);
        if (s is null) { s = new FulfillmentSla(); _db.Set<FulfillmentSla>().Add(s); }

        s.StoreId = input.StoreId; s.MaxHoursToShip = input.MaxHoursToShip; s.MaxHoursToDeliver = input.MaxHoursToDeliver;
        s.PenaltyNote = string.IsNullOrWhiteSpace(input.PenaltyNote) ? null : input.PenaltyNote.Trim();
        s.IsActive = input.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("fulfillment_sla.upsert", "FulfillmentSla", s.Id.ToString(), $"{s.MaxHoursToShip}h/{s.MaxHoursToDeliver}h", ct: ct);
        return Map(s);
    }

    public async Task DeleteConfigAsync(Guid id, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        var s = await _db.Set<FulfillmentSla>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("FulfillmentSla", id);
        _db.Set<FulfillmentSla>().Remove(s);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("fulfillment_sla.delete", "FulfillmentSla", id.ToString(), null, ct: ct);
    }

    public async Task<IReadOnlyList<OrderSlaDto>> GetOrderSlaAsync(Guid orderId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var order = await _db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments).Include(o => o.DeliveryAssignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct) ?? throw new NotFoundException("Order", orderId);

        var storeIds = order.Items.Select(i => i.StoreId).Distinct().ToList();
        var ownedStores = await _db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id) && s.OwnerUserId == uid)
            .Select(s => s.Id).ToListAsync(ct);
        var isStaff = _current.Permissions.Contains(Permissions.Orders.View);
        if (order.UserId != uid && !isStaff && ownedStores.Count == 0) throw new ForbiddenException();

        var slas = await SlaEvaluator.LoadSlasAsync(_db, ct);
        var times = await SlaEvaluator.LoadTimesAsync(_db, new[] { order }, ct);
        var now = DateTime.UtcNow;
        var result = new List<OrderSlaDto>();
        foreach (var storeId in storeIds)
        {
            var (ship, deliver) = SlaEvaluator.Targets(slas, storeId);
            var t = times[order.Id];
            var (s, d, overall) = SlaMath.Evaluate(t.Start, t.ShippedAt, t.DeliveredAt, now, ship, deliver);
            result.Add(new OrderSlaDto(order.Id, storeId, s, d, overall, t.Start,
                t.Start.AddHours(ship), t.Start.AddHours(deliver), t.ShippedAt, t.DeliveredAt));
        }
        return result;
    }

    private async Task<PageResult<SlaBreachDto>> PageAsync(IQueryable<SlaBreach> q, bool openOnly, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
        if (openOnly) q = q.Where(b => b.ResolvedAt == null);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(b => b.BreachedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
        var storeIds = rows.Select(r => r.StoreId).Distinct().ToList();
        var numbers = await _db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber, ct);
        var names = await _db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var items = rows.Select(r => new SlaBreachDto(r.Id, r.OrderId, numbers.GetValueOrDefault(r.OrderId), r.StoreId,
            names.GetValueOrDefault(r.StoreId), r.Kind, r.BreachedAt, r.ResolvedAt)).ToList();
        return new PageResult<SlaBreachDto>(items, total, page, pageSize);
    }

    public Task<PageResult<SlaBreachDto>> ListBreachesAsync(Guid? storeId, bool openOnly, int page, int pageSize, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Permissions.Settings.View) && !_current.Permissions.Contains(Permissions.Orders.View))
            throw new ForbiddenException();
        var q = _db.Set<SlaBreach>().AsNoTracking().AsQueryable();
        if (storeId.HasValue) q = q.Where(b => b.StoreId == storeId);
        return PageAsync(q, openOnly, page, pageSize, ct);
    }

    public async Task<PageResult<SlaBreachDto>> ListMyBreachesAsync(bool openOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var mine = await _db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid).Select(s => s.Id).ToListAsync(ct);
        return await PageAsync(_db.Set<SlaBreach>().AsNoTracking().Where(b => mine.Contains(b.StoreId)), openOnly, page, pageSize, ct);
    }
}

/// <summary>Shared SLA data loading (orders -> start / shipped / delivered instants, per-store targets).</summary>
internal static class SlaEvaluator
{
    public record OrderTimes(DateTime Start, DateTime? ShippedAt, DateTime? DeliveredAt);

    public static async Task<List<FulfillmentSla>> LoadSlasAsync(ApplicationDbContext db, CancellationToken ct) =>
        await db.Set<FulfillmentSla>().AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);

    public static (int Ship, int Deliver) Targets(IReadOnlyList<FulfillmentSla> slas, Guid storeId)
    {
        var s = slas.FirstOrDefault(x => x.StoreId == storeId) ?? slas.FirstOrDefault(x => x.StoreId == null);
        return s is null ? (SlaService.DefaultMaxHoursToShip, SlaService.DefaultMaxHoursToDeliver) : (s.MaxHoursToShip, s.MaxHoursToDeliver);
    }

    /// <summary>Orders must have Payments and DeliveryAssignment loaded.</summary>
    public static async Task<Dictionary<Guid, OrderTimes>> LoadTimesAsync(ApplicationDbContext db, IReadOnlyCollection<Order> orders, CancellationToken ct)
    {
        var ids = orders.Select(o => o.Id).ToList();
        var cps = await db.Set<DeliveryCheckpoint>().AsNoTracking()
            .Where(c => ids.Contains(c.OrderId) && (c.Status == TrackingStatus.PickedUp || c.Status == TrackingStatus.InTransit
                        || c.Status == TrackingStatus.OutForDelivery || c.Status == TrackingStatus.Delivered))
            .Select(c => new { c.OrderId, c.Status, c.OccurredAt }).ToListAsync(ct);

        var map = new Dictionary<Guid, OrderTimes>();
        foreach (var o in orders)
        {
            // Clock starts when the order was paid; cash on delivery (never "paid" up front) starts at placement.
            var paidAt = o.Payments.Where(p => p.Status == PaymentStatus.Paid).Select(p => (DateTime?)(p.UpdatedAt ?? p.CreatedAt)).Min();
            var start = paidAt ?? o.CreatedAt;

            var mine = cps.Where(c => c.OrderId == o.Id).ToList();
            DateTime? shipped = o.DeliveryAssignment?.PickedUpAt;
            if (shipped is null && mine.Count > 0) shipped = mine.Min(c => c.OccurredAt);
            if (shipped is null && o.ShipmentStatus >= ShipmentStatus.InTransit) shipped = o.UpdatedAt ?? o.CreatedAt;

            DateTime? delivered = o.DeliveryAssignment?.DeliveredAt;
            if (delivered is null) delivered = mine.Where(c => c.Status == TrackingStatus.Delivered).Select(c => (DateTime?)c.OccurredAt).Min();
            if (delivered is null && o.ShipmentStatus == ShipmentStatus.Delivered) delivered = o.UpdatedAt ?? o.CreatedAt;
            if (delivered is not null && shipped is null) shipped = delivered;

            map[o.Id] = new OrderTimes(start, shipped, delivered);
        }
        return map;
    }
}

/// <summary>
/// Worker-side monitor: records SlaBreach rows idempotently (unique order/store/kind), notifies the store owner
/// and admins, and stamps ResolvedAt once the late step eventually completes.
/// </summary>
public class SlaMonitor : ISlaMonitor
{
    private const int MaxOrdersPerRun = 2000;
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notify;
    private readonly ILogger<SlaMonitor> _log;

    public SlaMonitor(ApplicationDbContext db, INotificationService notify, ILogger<SlaMonitor> log)
    { _db = db; _notify = notify; _log = log; }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-60);
        var orders = await _db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.Payments).Include(o => o.DeliveryAssignment)
            .Where(o => o.CreatedAt >= since
                        && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Denied && o.Status != OrderStatus.Returned
                        && (o.PaymentStatus == PaymentStatus.Paid || o.Payments.Any(p => p.Method == "cod")))
            .OrderBy(o => o.CreatedAt).Take(MaxOrdersPerRun).ToListAsync(ct);
        if (orders.Count == 0) return 0;

        var slas = await SlaEvaluator.LoadSlasAsync(_db, ct);
        var times = await SlaEvaluator.LoadTimesAsync(_db, orders, ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var existing = await _db.Set<SlaBreach>().Where(b => orderIds.Contains(b.OrderId)).ToListAsync(ct);

        var fresh = new List<(SlaBreach Breach, string OrderNumber)>();
        foreach (var o in orders)
        {
            var t = times[o.Id];
            foreach (var storeId in o.Items.Select(i => i.StoreId).Distinct())
            {
                var (shipH, delH) = SlaEvaluator.Targets(slas, storeId);
                var (ship, deliver, _) = SlaMath.Evaluate(t.Start, t.ShippedAt, t.DeliveredAt, now, shipH, delH);
                Apply(o, storeId, SlaBreachKind.Ship, ship, t.ShippedAt.HasValue, now, existing, fresh);
                Apply(o, storeId, SlaBreachKind.Deliver, deliver, t.DeliveredAt.HasValue, now, existing, fresh);
            }
        }

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // A concurrent monitor run won the unique index: drop our inserts, the rows already exist.
            _log.LogWarning(ex, "SLA monitor lost a race recording breaches; skipping notifications this run");
            foreach (var e in _db.ChangeTracker.Entries<SlaBreach>().Where(e => e.State == EntityState.Added).ToList())
                e.State = EntityState.Detached;
            return 0;
        }

        if (fresh.Count > 0) await NotifyAsync(fresh, ct);
        return fresh.Count;
    }

    private void Apply(Order o, Guid storeId, SlaBreachKind kind, SlaStatus status, bool done, DateTime now,
        List<SlaBreach> existing, List<(SlaBreach, string)> fresh)
    {
        var row = existing.FirstOrDefault(b => b.OrderId == o.Id && b.StoreId == storeId && b.Kind == kind);
        if (status == SlaStatus.Breached)
        {
            if (row is null)
            {
                row = new SlaBreach { OrderId = o.Id, StoreId = storeId, Kind = kind, BreachedAt = now };
                existing.Add(row);
                // Orders already completed late are recorded (history) but resolved immediately and not announced.
                if (done) row.ResolvedAt = now; else fresh.Add((row, o.OrderNumber));
                _db.Set<SlaBreach>().Add(row);
            }
            else if (done && row.ResolvedAt is null) row.ResolvedAt = now;
        }
        else if (row is not null && row.ResolvedAt is null && done) row.ResolvedAt = now;
    }

    private async Task NotifyAsync(List<(SlaBreach Breach, string OrderNumber)> fresh, CancellationToken ct)
    {
        var storeIds = fresh.Select(f => f.Breach.StoreId).Distinct().ToList();
        var owners = await _db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.OwnerUserId, s.Name }).ToDictionaryAsync(s => s.Id, ct);
        foreach (var f in fresh)
        {
            if (!owners.TryGetValue(f.Breach.StoreId, out var st)) continue;
            var what = f.Breach.Kind == SlaBreachKind.Ship ? "shipping" : "delivery";
            try
            {
                await _notify.NotifyUserAsync(st.OwnerUserId, "Fulfillment SLA breached",
                    $"Order {f.OrderNumber} is past its {what} target. Please act now.",
                    new { orderId = f.Breach.OrderId, kind = f.Breach.Kind.ToString() }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "SLA owner notification failed"); }
        }

        var adminIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Name == SystemRoles.Admin || ur.Role.Name == SystemRoles.SuperAdmin)
            .Select(ur => ur.UserId).Distinct().Take(25).ToListAsync(ct);
        foreach (var admin in adminIds)
        {
            try
            {
                await _notify.NotifyUserAsync(admin, "Fulfillment SLA breaches",
                    $"{fresh.Count} new SLA breach(es) across {storeIds.Count} store(s).", new { count = fresh.Count }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogWarning(ex, "SLA admin notification failed"); }
        }
    }
}
