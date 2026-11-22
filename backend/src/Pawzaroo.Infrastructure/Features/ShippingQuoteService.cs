using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Zone based shipping quotes. Config: Logistics:UtcOffsetMinutes (default 360 = UTC+6) is the local clock used
/// for peak-hour windows; Settlement:Currency labels the amounts.
/// </summary>
public class ShippingQuoteService : IShippingQuoteService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public ShippingQuoteService(ApplicationDbContext db, IConfiguration config) { _db = db; _config = config; }

    private string Currency => string.IsNullOrWhiteSpace(_config["Settlement:Currency"]) ? "BDT" : _config["Settlement:Currency"]!;

    private DateTime ToLocal(DateTime utc)
    {
        var off = int.TryParse(_config["Logistics:UtcOffsetMinutes"], out var m) ? m : 360;
        var u = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
        return u.AddMinutes(off);
    }

    public async Task<ShippingQuoteDto> QuoteAsync(Guid? storeId, string? destinationCity, decimal subtotal, DateTime at, CancellationToken ct = default)
    {
        var zones = await _db.Set<DeliveryZone>().AsNoTracking()
            .Where(z => z.IsActive && (z.StoreId == null || z.StoreId == storeId)).ToListAsync(ct);
        // A store-specific zone wins over a platform-wide one.
        var zone = zones.Where(z => ShippingQuoteMath.CityMatches(z.Cities, destinationCity))
            .OrderByDescending(z => z.StoreId != null).ThenBy(z => z.BaseFee).FirstOrDefault();
        if (zone is null)
            return new ShippingQuoteDto(false, null, null, Currency, 0m, 0m, null, false, 0m, null, null, null, null);

        var local = ToLocal(at);
        var rules = await _db.Set<PeakHourRule>().AsNoTracking()
            .Where(r => r.IsActive && (r.ZoneId == null || r.ZoneId == zone.Id)).ToListAsync(ct);
        // Overlapping rules do not stack: the largest surcharge applies.
        PeakHourRule? bestRule = null;
        var bestAmount = 0m;
        foreach (var r in rules.Where(r => ShippingQuoteMath.IsInWindow(r.DaysOfWeek, r.StartTime, r.EndTime, local)))
        {
            var amt = ShippingQuoteMath.Surcharge(zone.BaseFee, r.SurchargeFixed, r.SurchargePercent);
            if (bestRule is null || amt > bestAmount) { bestRule = r; bestAmount = amt; }
        }

        var (b, p, free, total) = ShippingQuoteMath.Compute(zone.BaseFee, zone.FreeShippingThreshold, subtotal, bestAmount);
        var (early, late) = ShippingQuoteMath.Eta(at, zone.EstimatedMinHours, zone.EstimatedMaxHours);
        return new ShippingQuoteDto(true, zone.Id, zone.Name, Currency, b, p, p > 0m ? bestRule?.Label ?? "Peak hours" : null,
            free, total, zone.EstimatedMinHours, zone.EstimatedMaxHours, early, late);
    }

    public async Task<decimal?> TryQuoteOrderFeeAsync(IReadOnlyList<(Guid StoreId, decimal NetSubtotal)> stores,
        string? destinationCity, DateTime at, CancellationToken ct = default)
    {
        if (stores.Count == 0 || string.IsNullOrWhiteSpace(destinationCity)) return null;
        decimal sum = 0m;
        foreach (var s in stores)
        {
            var q = await QuoteAsync(s.StoreId, destinationCity, s.NetSubtotal, at, ct);
            if (!q.ZoneMatched) return null;   // all-or-nothing: keep the legacy fee logic
            sum += q.Total;
        }
        return sum;
    }
}

/// <summary>Admin CRUD for zones and peak-hour rules (Settings.View / Settings.Edit).</summary>
public class DeliveryZoneAdminService : IDeliveryZoneAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditLogger _audit;

    public DeliveryZoneAdminService(ApplicationDbContext db, ICurrentUserService current, IAuditLogger audit)
    { _db = db; _current = current; _audit = audit; }

    private void Need(string perm) { if (!_current.Permissions.Contains(perm)) throw new ForbiddenException(); }

    private static DeliveryZoneDto Map(DeliveryZone z) => new(z.Id, z.StoreId, z.Name,
        z.Cities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        z.BaseFee, z.FreeShippingThreshold, z.EstimatedMinHours, z.EstimatedMaxHours, z.IsActive);

    private static PeakHourRuleDto Map(PeakHourRule r) => new(r.Id, r.ZoneId, ShippingQuoteMath.MaskToDays(r.DaysOfWeek),
        r.StartTime.ToString(@"hh\:mm"), r.EndTime.ToString(@"hh\:mm"), r.SurchargeFixed, r.SurchargePercent, r.IsActive, r.Label);

    private static ValidationException Invalid(string field, string msg) =>
        new(new Dictionary<string, string[]> { [field] = new[] { msg } });

    public async Task<IReadOnlyList<DeliveryZoneDto>> ListZonesAsync(CancellationToken ct = default)
    {
        Need(Permissions.Settings.View);
        var rows = await _db.Set<DeliveryZone>().AsNoTracking().OrderBy(z => z.Name).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<DeliveryZoneDto> UpsertZoneAsync(Guid? id, UpsertDeliveryZoneInput input, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        if (string.IsNullOrWhiteSpace(input.Name)) throw Invalid("name", "Name is required.");
        var cities = input.Cities.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (cities.Count == 0) throw Invalid("cities", "At least one city is required.");
        if (cities.Any(c => c.Contains(','))) throw Invalid("cities", "City names cannot contain commas.");
        if (input.BaseFee < 0m) throw Invalid("baseFee", "Base fee cannot be negative.");
        if (input.FreeShippingThreshold is < 0m) throw Invalid("freeShippingThreshold", "Threshold cannot be negative.");
        if (input.EstimatedMinHours < 0 || input.EstimatedMaxHours < input.EstimatedMinHours)
            throw Invalid("estimatedMaxHours", "ETA range is invalid.");

        DeliveryZone z;
        if (id is { } zid)
            z = await _db.Set<DeliveryZone>().FirstOrDefaultAsync(x => x.Id == zid, ct) ?? throw new NotFoundException("DeliveryZone", zid);
        else { z = new DeliveryZone(); _db.Set<DeliveryZone>().Add(z); }

        z.StoreId = input.StoreId; z.Name = input.Name.Trim(); z.Cities = string.Join(",", cities);
        z.BaseFee = ShippingQuoteMath.Round(input.BaseFee);
        z.FreeShippingThreshold = input.FreeShippingThreshold is > 0m ? ShippingQuoteMath.Round(input.FreeShippingThreshold.Value) : null;
        z.EstimatedMinHours = input.EstimatedMinHours; z.EstimatedMaxHours = input.EstimatedMaxHours; z.IsActive = input.IsActive;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "delivery_zone.create" : "delivery_zone.update", "DeliveryZone", z.Id.ToString(), z.Name, ct: ct);
        return Map(z);
    }

    public async Task DeleteZoneAsync(Guid id, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        var z = await _db.Set<DeliveryZone>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("DeliveryZone", id);
        _db.Set<DeliveryZone>().Remove(z);   // soft delete via SaveChanges
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("delivery_zone.delete", "DeliveryZone", id.ToString(), z.Name, ct: ct);
    }

    public async Task<IReadOnlyList<PeakHourRuleDto>> ListRulesAsync(CancellationToken ct = default)
    {
        Need(Permissions.Settings.View);
        var rows = await _db.Set<PeakHourRule>().AsNoTracking().OrderBy(r => r.StartTime).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<PeakHourRuleDto> UpsertRuleAsync(Guid? id, UpsertPeakHourRuleInput input, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        if (!TimeSpan.TryParse(input.StartTime, out var start) || start < TimeSpan.Zero || start >= TimeSpan.FromDays(1))
            throw Invalid("startTime", "Start time must be HH:mm.");
        if (!TimeSpan.TryParse(input.EndTime, out var end) || end < TimeSpan.Zero || end >= TimeSpan.FromDays(1))
            throw Invalid("endTime", "End time must be HH:mm.");
        if (start == end) throw Invalid("endTime", "Start and end time must differ (overnight windows are allowed).");
        var mask = ShippingQuoteMath.DaysToMask(input.Days);
        if (mask == 0) throw Invalid("days", "Choose at least one day.");
        if (input.SurchargeFixed < 0m || input.SurchargePercent < 0m || input.SurchargePercent > 500m)
            throw Invalid("surchargeFixed", "Surcharge is invalid.");
        if (input.SurchargeFixed == 0m && input.SurchargePercent == 0m) throw Invalid("surchargeFixed", "Set a fixed or percent surcharge.");
        if (input.ZoneId is { } zoneId && !await _db.Set<DeliveryZone>().AnyAsync(x => x.Id == zoneId, ct))
            throw new NotFoundException("DeliveryZone", zoneId);

        PeakHourRule r;
        if (id is { } rid)
            r = await _db.Set<PeakHourRule>().FirstOrDefaultAsync(x => x.Id == rid, ct) ?? throw new NotFoundException("PeakHourRule", rid);
        else { r = new PeakHourRule(); _db.Set<PeakHourRule>().Add(r); }

        r.ZoneId = input.ZoneId; r.DaysOfWeek = mask; r.StartTime = start; r.EndTime = end;
        r.SurchargeFixed = ShippingQuoteMath.Round(input.SurchargeFixed);
        r.SurchargePercent = ShippingQuoteMath.Round(input.SurchargePercent);
        r.IsActive = input.IsActive; r.Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(id is null ? "peak_rule.create" : "peak_rule.update", "PeakHourRule", r.Id.ToString(), r.Label, ct: ct);
        return Map(r);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        Need(Permissions.Settings.Edit);
        var r = await _db.Set<PeakHourRule>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("PeakHourRule", id);
        _db.Set<PeakHourRule>().Remove(r);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("peak_rule.delete", "PeakHourRule", id.ToString(), null, ct: ct);
    }
}
