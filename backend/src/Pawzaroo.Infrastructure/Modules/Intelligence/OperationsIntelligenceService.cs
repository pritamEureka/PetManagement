using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Intelligence;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Intelligence;

/// <summary>
/// Read-only statistical views over operational data. Heavy lifting is done by
/// projecting narrow rows from Postgres and aggregating in memory with <see cref="Stats"/>.
/// </summary>
public class OperationsIntelligenceService : IOperationsIntelligenceService
{
    private const int ForecastHorizonDays = 14;
    private readonly ApplicationDbContext _db;

    public OperationsIntelligenceService(ApplicationDbContext db) => _db = db;

    private static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateTime WeekStart(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static double[] DailySeries(IEnumerable<DateTime> dates, DateTime from, int days)
    {
        var counts = dates.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, days).Select(i => counts.TryGetValue(from.AddDays(i), out var c) ? (double)c : 0d).ToArray();
    }

    // ---------------- Demand forecast ----------------

    public async Task<DemandForecastDto> GetDemandForecastAsync(int historyDays, CancellationToken ct = default)
    {
        historyDays = Math.Clamp(historyDays, 14, 180);
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(historyDays - 1));
        var dates = await _db.Appointments.AsNoTracking()
            .Where(a => a.ScheduledAt >= from && a.ScheduledAt < today.AddDays(1) && a.Status != AppointmentStatus.Draft)
            .Select(a => a.ScheduledAt).ToListAsync(ct);

        var actual = DailySeries(dates, from, historyDays);
        var ma = Stats.MovingAverage(actual, 7);
        var (slope, _) = Stats.LinearRegression(actual);
        var projected = Stats.ProjectLinear(actual, ForecastHorizonDays);

        var points = new List<DemandPoint>();
        for (var i = 0; i < historyDays; i++)
            points.Add(new DemandPoint(D(from.AddDays(i)), actual[i], Math.Round(ma[i], 2), null));
        for (var k = 0; k < projected.Length; k++)
            points.Add(new DemandPoint(D(today.AddDays(k + 1)), null, null, Math.Round(projected[k], 2)));
        return new DemandForecastDto(points, Math.Round(slope, 4), Math.Round(projected.Sum(), 1));
    }

    // ---------------- No-show / cancellation ----------------

    public async Task<IReadOnlyList<DoctorWeekRateDto>> GetNoShowRatesAsync(int weeks, CancellationToken ct = default)
    {
        weeks = Math.Clamp(weeks, 1, 26);
        var now = DateTime.UtcNow;
        var from = WeekStart(now).AddDays(-7 * (weeks - 1));
        var rows = await _db.Appointments.AsNoTracking()
            .Where(a => a.ScheduledAt >= from && a.ScheduledAt <= now && a.Status != AppointmentStatus.Draft
                        && a.Status != AppointmentStatus.PendingPayment)
            .Select(a => new { a.DoctorId, Name = a.Doctor.User.DisplayName, a.ScheduledAt, a.Status })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { Week = WeekStart(r.ScheduledAt), r.DoctorId, r.Name })
            .Select(g =>
            {
                var total = g.Count();
                var noShow = g.Count(x => x.Status == AppointmentStatus.NoShow);
                var cancelled = g.Count(x => x.Status is AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor);
                return new DoctorWeekRateDto(D(g.Key.Week), g.Key.DoctorId, g.Key.Name, total, noShow, cancelled,
                    Stats.Rate(noShow, total), Stats.Rate(cancelled, total));
            })
            .OrderBy(x => x.WeekStart).ThenBy(x => x.DoctorName)
            .ToList();
    }

    // ---------------- Capacity ----------------

    public async Task<IReadOnlyList<CapacityPointDto>> GetCapacityAsync(int daysBack, int daysAhead, CancellationToken ct = default)
    {
        daysBack = Math.Clamp(daysBack, 0, 90);
        daysAhead = Math.Clamp(daysAhead, 0, 60);
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-daysBack);
        var to = today.AddDays(daysAhead + 1);
        var rows = await _db.DoctorTimeSlots.AsNoTracking()
            .Where(s => s.StartUtc >= from && s.StartUtc < to
                        && (s.Status == SlotStatus.Available || s.Status == SlotStatus.Held || s.Status == SlotStatus.Booked))
            .Select(s => new { s.StartUtc, s.Status })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.StartUtc.Date).OrderBy(g => g.Key)
            .Select(g =>
            {
                var booked = g.Count(x => x.Status == SlotStatus.Booked);
                var cap = g.Count();
                return new CapacityPointDto(D(g.Key), booked, cap, Stats.Rate(booked, cap));
            }).ToList();
    }

    // ---------------- Vaccine coverage ----------------

    public async Task<IReadOnlyList<VaccineCoverageDto>> GetVaccineCoverageAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pets = await _db.Pets.AsNoTracking().Select(p => new { p.Id, p.AnimalType }).ToListAsync(ct);
        var vaccinated = (await _db.VaccinationRecords.AsNoTracking().Select(v => v.PetId).Distinct().ToListAsync(ct)).ToHashSet();
        // A record is overdue when its due date has passed and no later dose of the same vaccine exists.
        var overdue = (await _db.VaccinationRecords.AsNoTracking()
            .Where(v => v.NextDueOn != null && v.NextDueOn < now
                        && !_db.VaccinationRecords.Any(o => o.PetId == v.PetId && o.VaccineName == v.VaccineName
                                                            && o.AdministeredOn > v.AdministeredOn))
            .Select(v => v.PetId).Distinct().ToListAsync(ct)).ToHashSet();

        return pets.GroupBy(p => p.AnimalType).OrderBy(g => g.Key)
            .Select(g =>
            {
                var upToDate = g.Count(p => vaccinated.Contains(p.Id) && !overdue.Contains(p.Id));
                return new VaccineCoverageDto(g.Key.ToString(), g.Count(), upToDate, Stats.Rate(upToDate, g.Count()));
            }).ToList();
    }

    // ---------------- Lost pet recovery ----------------

    public async Task<IReadOnlyList<LostPetWeekDto>> GetLostPetTrendAsync(int weeks, CancellationToken ct = default)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var from = WeekStart(DateTime.UtcNow).AddDays(-7 * (weeks - 1));
        var rows = await _db.LostFoundReports.AsNoTracking()
            .Where(r => r.Kind == LostFoundKind.Lost && r.CreatedAt >= from)
            .Select(r => new { r.CreatedAt, r.Status })
            .ToListAsync(ct);

        var result = new List<LostPetWeekDto>();
        for (var w = 0; w < weeks; w++)
        {
            var start = from.AddDays(7 * w);
            var inWeek = rows.Where(r => WeekStart(r.CreatedAt) == start).ToList();
            var reunited = inWeek.Count(r => r.Status == LostFoundStatus.Reunited);
            var open = inWeek.Count(r => r.Status == LostFoundStatus.Open);
            result.Add(new LostPetWeekDto(D(start), inWeek.Count, reunited, open, Stats.Rate(reunited, inWeek.Count)));
        }
        return result;
    }

    // ---------------- Store conversion & returns ----------------

    public async Task<IReadOnlyList<StoreTrendWeekDto>> GetStoreTrendsAsync(int weeks, CancellationToken ct = default)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var from = WeekStart(DateTime.UtcNow).AddDays(-7 * (weeks - 1));
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Denied)
            .Select(o => new { o.UserId, o.CreatedAt }).ToListAsync(ct);
        var carts = await _db.CartItems.AsNoTracking()
            .Where(c => c.AddedAt >= from).Select(c => new { c.UserId, c.AddedAt }).ToListAsync(ct);
        var returns = await _db.ReturnRequests.AsNoTracking()
            .Where(r => r.CreatedAt >= from).Select(r => r.CreatedAt).ToListAsync(ct);

        var result = new List<StoreTrendWeekDto>();
        for (var w = 0; w < weeks; w++)
        {
            var start = from.AddDays(7 * w);
            var o = orders.Where(x => WeekStart(x.CreatedAt) == start).ToList();
            var cartUsers = carts.Where(x => WeekStart(x.AddedAt) == start).Select(x => x.UserId).Distinct().Count();
            var orderUsers = o.Select(x => x.UserId).Distinct().Count();
            var ret = returns.Count(x => WeekStart(x) == start);
            var conversion = cartUsers == 0 ? 0 : Math.Min(100, Stats.Rate(orderUsers, cartUsers));
            result.Add(new StoreTrendWeekDto(D(start), o.Count, cartUsers, orderUsers, conversion, ret, Stats.Rate(ret, o.Count)));
        }
        return result;
    }

    // ---------------- Inventory run-out ----------------

    public async Task<IReadOnlyList<InventoryRunoutDto>> GetInventoryRunoutAsync(int salesWindowDays, CancellationToken ct = default)
    {
        salesWindowDays = Math.Clamp(salesWindowDays, 7, 90);
        var from = DateTime.UtcNow.Date.AddDays(-salesWindowDays);
        var sales = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Order.CreatedAt >= from && i.Order.Status != OrderStatus.Cancelled
                        && i.Order.Status != OrderStatus.Denied && i.Order.Status != OrderStatus.Returned)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(x => (long)x.Quantity) })
            .ToListAsync(ct);
        if (sales.Count == 0) return Array.Empty<InventoryRunoutDto>();

        var unitsById = sales.ToDictionary(s => s.ProductId, s => s.Units);
        var ids = unitsById.Keys.ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Sku, p.StockQuantity })
            .ToListAsync(ct);
        var today = DateTime.UtcNow.Date;

        return products.Select(p =>
            {
                var avg = unitsById[p.Id] / (double)salesWindowDays;
                double? days = avg <= 0 ? null : Math.Round(p.StockQuantity / avg, 1);
                string? date = days is { } d && d < 3650 ? D(today.AddDays(Math.Ceiling(d))) : null;
                return new InventoryRunoutDto(p.Id, p.Name, p.Sku, p.StockQuantity, Math.Round(avg, 2), days, date);
            })
            .OrderBy(x => x.DaysRemaining ?? double.MaxValue)
            .Take(100).ToList();
    }

    // ---------------- Anomalies ----------------

    public async Task<IReadOnlyList<AnomalyDto>> GetAnomaliesAsync(int days, double threshold, CancellationToken ct = default)
    {
        days = Math.Clamp(days, 14, 180);
        threshold = Math.Clamp(threshold, 1, 6);
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(days - 1));
        var orderDates = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.Status != OrderStatus.Cancelled).Select(o => o.CreatedAt).ToListAsync(ct);
        var aptDates = await _db.Appointments.AsNoTracking()
            .Where(a => a.ScheduledAt >= from && a.ScheduledAt < today.AddDays(1) && a.Status != AppointmentStatus.Draft)
            .Select(a => a.ScheduledAt).ToListAsync(ct);

        var result = new List<AnomalyDto>();
        void Scan(string metric, double[] series)
        {
            var z = Stats.ZScores(series);
            for (var i = 0; i < series.Length; i++)
                if (Math.Abs(z[i]) >= threshold)
                    result.Add(new AnomalyDto(metric, D(from.AddDays(i)), series[i], Math.Round(z[i], 2), z[i] > 0 ? "spike" : "drop"));
        }
        Scan("orders", DailySeries(orderDates, from, days));
        Scan("appointments", DailySeries(aptDates, from, days));
        return result.OrderByDescending(a => a.Date).ThenBy(a => a.Metric).ToList();
    }

    // ---------------- CSV export ----------------

    public async Task<string?> ExportCsvAsync(string dataset, CancellationToken ct = default)
    {
        switch (dataset.ToLowerInvariant())
        {
            case "demand": return ToCsv((await GetDemandForecastAsync(60, ct)).Points);
            case "no-show": return ToCsv(await GetNoShowRatesAsync(12, ct));
            case "capacity": return ToCsv(await GetCapacityAsync(14, 14, ct));
            case "vaccine-coverage": return ToCsv(await GetVaccineCoverageAsync(ct));
            case "lost-pets": return ToCsv(await GetLostPetTrendAsync(12, ct));
            case "store": return ToCsv(await GetStoreTrendsAsync(12, ct));
            case "inventory": return ToCsv(await GetInventoryRunoutAsync(30, ct));
            case "anomalies": return ToCsv(await GetAnomaliesAsync(60, 2.5, ct));
            default: return null;
        }
    }

    internal static string ToCsv<T>(IEnumerable<T> rows)
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => p.Name)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", props.Select(p => Escape(p.GetValue(row)))));
        return sb.ToString();
    }

    private static string Escape(object? v)
    {
        var s = v switch
        {
            null => "",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString() ?? ""
        };
        // Neutralise spreadsheet formula injection (text only; plain negative numbers are left alone).
        var isNumber = v is IFormattable and not DateTime;
        if (s.Length > 0 && !isNumber && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@'))
            s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
