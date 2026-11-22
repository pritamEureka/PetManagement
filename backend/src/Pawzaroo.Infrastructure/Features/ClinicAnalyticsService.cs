using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.ClinicPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Veterinary;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>Analytics for the signed-in doctor's own practice (not an admin view).</summary>
public class ClinicAnalyticsService : IClinicAnalyticsService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _config;

    public ClinicAnalyticsService(ApplicationDbContext db, ICurrentUserService current, Microsoft.Extensions.Configuration.IConfiguration? config = null)
    { _db = db; _current = current; _config = config; }

    public static string PeriodKey(DateTime d, string g) => g switch
    {
        "day" => d.ToString("yyyy-MM-dd"),
        "week" => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd"),
        _ => d.ToString("yyyy-MM")
    };

    public async Task<ClinicAnalyticsDto> MineAsync(DateOnly? from, DateOnly? to, string? granularity, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var did = await _db.Doctors.Where(d => d.UserId == uid).Select(d => (Guid?)d.Id).SingleOrDefaultAsync(ct)
                  ?? throw new ForbiddenException("Only registered veterinarians have clinic analytics.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var t = to ?? today;
        var f = from ?? t.AddDays(-89);
        if (f > t) (f, t) = (t, f);
        if (t.DayNumber - f.DayNumber > 366) f = t.AddDays(-366);
        var g = granularity is "day" or "week" ? granularity : "month";
        var fromDt = f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDt = t.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var appts = await _db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == did && a.ScheduledAt >= fromDt && a.ScheduledAt < toDt)
            .Select(a => new { a.ScheduledAt, a.Status, a.PaymentStatus, a.Amount }).ToListAsync(ct);

        bool Cancelled(AppointmentStatus s) => s is AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor;
        decimal Rev(AppointmentStatus s, PaymentStatus p, decimal amt) => s == AppointmentStatus.Completed && p == PaymentStatus.Paid ? amt : 0;

        var series = appts.GroupBy(a => PeriodKey(a.ScheduledAt, g)).OrderBy(x => x.Key)
            .Select(x => new AnalyticsPoint(x.Key, x.Sum(a => Rev(a.Status, a.PaymentStatus, a.Amount)), x.Count(),
                x.Count(a => a.Status == AppointmentStatus.Completed), x.Count(a => a.Status == AppointmentStatus.NoShow),
                x.Count(a => Cancelled(a.Status)))).ToList();

        var completed = appts.Count(a => a.Status == AppointmentStatus.Completed);
        var noShows = appts.Count(a => a.Status == AppointmentStatus.NoShow);
        var cancelled = appts.Count(a => Cancelled(a.Status));
        var seen = completed + noShows;

        var slots = await _db.DoctorTimeSlots.AsNoTracking()
            .Where(s => s.DoctorId == did && s.StartUtc >= fromDt && s.StartUtc < toDt && s.Status != SlotStatus.Blocked && s.Status != SlotStatus.Cancelled)
            .Select(s => s.Status).ToListAsync(ct);
        var offered = slots.Count;
        var booked = slots.Count(s => s == SlotStatus.Booked);

        // Patient-return rate over the trailing year of completed visits.
        var yearAgo = DateTime.UtcNow.AddDays(-365);
        var visits = await _db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == did && a.Status == AppointmentStatus.Completed && a.ScheduledAt >= yearAgo)
            .Select(a => new { a.PatientUserId, a.ScheduledAt }).ToListAsync(ct);
        var pairs = visits.Select(v => (v.PatientUserId, v.ScheduledAt)).ToList();
        var r90 = ReturnRateMath.Compute(pairs, 90);
        var r180 = ReturnRateMath.Compute(pairs, 180);

        var clinicRows = await ClinicRowsAsync(did, fromDt, toDt, ct);

        return new ClinicAnalyticsDto(f, t, g, series.Sum(s => s.Revenue), appts.Count, completed, noShows, cancelled,
            seen == 0 ? 0 : Math.Round((double)noShows / seen, 4), offered == 0 ? 0 : Math.Round((double)booked / offered, 4), offered, booked,
            r90.Rate, r90.Returning, r180.Rate, r180.Returning, r90.Patients, series, clinicRows);
    }

    /// <summary>
    /// Doctors sharing the caller's DoctorEmergencyProfile.ClinicName (trimmed, case-insensitive, non-empty) form one clinic.
    /// Returns the caller plus colleagues (caller first); empty when the caller has no clinic name. Pure, unit-tested.
    /// Note: ClinicName is self-declared, so this is a convenience view, not a verified organisation boundary.
    /// </summary>
    public static IReadOnlyList<Guid> ClinicMates(IEnumerable<(Guid DoctorId, string? ClinicName)> profiles, Guid me)
    {
        var list = profiles.ToList();
        var mine = list.FirstOrDefault(p => p.DoctorId == me).ClinicName?.Trim();
        if (string.IsNullOrEmpty(mine)) return Array.Empty<Guid>();
        return list.Where(p => p.DoctorId != me && string.Equals(p.ClinicName?.Trim(), mine, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DoctorId).Distinct().Prepend(me).ToList();
    }

    /// <summary>Per-doctor aggregates (with a trailing totals-free list; the UI sums) for the caller's clinic, or null when not in a multi-doctor clinic.</summary>
    public static IReadOnlyList<ClinicDoctorRow> BuildRows(IEnumerable<(Guid DoctorId, string Name, AppointmentStatus Status, PaymentStatus Payment, decimal Amount)> appts,
        IReadOnlyList<(Guid DoctorId, string Name)> doctors)
    {
        var all = appts.ToList();
        return doctors.Select(d =>
        {
            var mine = all.Where(a => a.DoctorId == d.DoctorId).ToList();
            var rev = mine.Where(a => a.Status == AppointmentStatus.Completed && a.Payment == PaymentStatus.Paid).Sum(a => a.Amount);
            var completed = mine.Count(a => a.Status == AppointmentStatus.Completed);
            var noShows = mine.Count(a => a.Status == AppointmentStatus.NoShow);
            var seen = completed + noShows;
            return new ClinicDoctorRow(d.DoctorId, d.Name, rev, mine.Count, seen == 0 ? 0 : Math.Round((double)noShows / seen, 4));
        }).OrderByDescending(r => r.Revenue).ThenBy(r => r.DoctorName).ToList();
    }

    private async Task<IReadOnlyList<ClinicDoctorRow>?> ClinicRowsAsync(Guid me, DateTime fromDt, DateTime toDt, CancellationToken ct)
    {
        // Clinics are grouped by a SELF-DECLARED name, so anyone could type another clinic's name and read its revenue and
        // no-show rates. Off unless explicitly enabled (e.g. once clinics are admin-verified).
        if (!string.Equals(_config?["Clinical:ClinicAnalyticsEnabled"], "true", StringComparison.OrdinalIgnoreCase)) return null;
        var myClinic = await _db.Set<DoctorEmergencyProfile>().AsNoTracking().Where(p => p.DoctorId == me).Select(p => p.ClinicName).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(myClinic)) return null;
        var lowered = myClinic.Trim().ToLower();
        var profiles = await _db.Set<DoctorEmergencyProfile>().AsNoTracking()
            .Where(p => p.ClinicName != null && p.ClinicName.Trim().ToLower() == lowered)
            .Select(p => new { p.DoctorId, p.ClinicName }).ToListAsync(ct);
        var mates = ClinicMates(profiles.Select(p => (p.DoctorId, p.ClinicName)).Append((me, myClinic)).DistinctBy(p => p.Item1), me);
        if (mates.Count < 2) return null;

        var docs = await _db.Doctors.AsNoTracking().Where(d => mates.Contains(d.Id))
            .Select(d => new { d.Id, Name = d.User.DisplayName }).ToListAsync(ct);
        var appts = await _db.Appointments.AsNoTracking()
            .Where(a => mates.Contains(a.DoctorId) && a.ScheduledAt >= fromDt && a.ScheduledAt < toDt)
            .Select(a => new { a.DoctorId, a.Status, a.PaymentStatus, a.Amount }).ToListAsync(ct);
        return BuildRows(appts.Select(a => (a.DoctorId, "", a.Status, a.PaymentStatus, a.Amount)),
            docs.Select(d => (d.Id, d.Name)).ToList());
    }
}
