using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Pets.Events;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Pet-care reminders, every 5 minutes:
///   * vaccinations — 7 days before, 1 day before and once when overdue
///     (stage persisted on the record so each fires exactly once);
///   * medication doses — at each dose time unless the dose was already logged;
///   * daily (after 09:00 local): birthdays and expected births in 3 days;
///   * confirmed service bookings starting in ~24h.
/// Recipients are the owner plus co-owners / caretakers. Rows are written to
/// the notifications table directly — the worker is the notifications writer.
/// </summary>
public class PetCareReminderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    /// <summary>Don't nag about doses that were due long before reminders existed (seed / imported data).</summary>
    private static readonly TimeSpan StaleOverdue = TimeSpan.FromDays(60);

    private readonly IServiceProvider _sp;
    private readonly ILogger<PetCareReminderJob> _logger;
    private readonly TimeZoneInfo _zone;
    private DateTime _lastRunUtc = DateTime.UtcNow - Interval;
    private DateOnly? _lastDailyRun;

    public PetCareReminderJob(IServiceProvider sp, ILogger<PetCareReminderJob> logger, IConfiguration config)
    {
        _sp = sp;
        _logger = logger;
        var zoneId = config["Reminders:TimeZone"] ?? "Asia/Dhaka";
        try { _zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            _logger.LogWarning("Unknown Reminders:TimeZone '{Zone}', falling back to UTC", zoneId);
            _zone = TimeZoneInfo.Utc;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var kafka = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

                await VaccinationsAsync(db, kafka, now, stoppingToken);
                await MedicationDosesAsync(db, _lastRunUtc, now, stoppingToken);
                await ServiceBookingsAsync(db, now, stoppingToken);

                var local = TimeZoneInfo.ConvertTimeFromUtc(now, _zone);
                var today = DateOnly.FromDateTime(local);
                if (local.Hour >= 9 && _lastDailyRun != today)
                {
                    await BirthdaysAsync(db, today, stoppingToken);
                    await BreedingAsync(db, now, stoppingToken);
                    _lastDailyRun = today;
                }
                _lastRunUtc = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PetCareReminderJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    // ---------- Vaccinations ----------

    private async Task VaccinationsAsync(ApplicationDbContext db, IKafkaProducer kafka, DateTime now, CancellationToken ct)
    {
        var horizon = now.Date.AddDays(8);
        var staleBefore = now - StaleOverdue;
        var due = await db.VaccinationRecords
            .Where(v => v.NextDueOn != null && v.NextDueOn < horizon && v.NextDueOn > staleBefore
                     && (v.LastReminderStage == null || v.LastReminderStage > 0)
                     // superseded by a newer dose of the same vaccine → nothing to remind
                     && !db.VaccinationRecords.Any(o => o.PetId == v.PetId && o.Id != v.Id
                            && o.VaccineName.ToLower() == v.VaccineName.ToLower() && o.AdministeredOn > v.AdministeredOn))
            .Select(v => new { Record = v, PetName = v.Pet.Name, v.Pet.OwnerId })
            .Take(500)
            .ToListAsync(ct);

        foreach (var d in due)
        {
            var daysLeft = (d.Record.NextDueOn!.Value.Date - now.Date).Days;
            var stage = daysLeft <= 0 ? 0 : daysLeft <= 1 ? 1 : 7;
            if (d.Record.LastReminderStage is { } last && last <= stage) continue;

            var (title, body) = stage switch
            {
                0 => ($"{d.PetName}'s {d.Record.VaccineName} is overdue", $"It was due on {d.Record.NextDueOn:dd MMM yyyy}. Book a vet visit."),
                1 => ($"{d.PetName}'s {d.Record.VaccineName} is due tomorrow", "Book a vet visit if you haven't already."),
                _ => ($"{d.PetName}'s {d.Record.VaccineName} is due soon", $"Due on {d.Record.NextDueOn:dd MMM yyyy}.")
            };
            foreach (var uid in await RecipientsAsync(db, d.Record.PetId, d.OwnerId, ct))
                db.Notifications.Add(new InAppNotification { UserId = uid, Title = title, Body = body, Url = $"/pets/{d.Record.PetId}?tab=health" });

            d.Record.LastReminderStage = stage;
            await kafka.PublishAsync(PetTopics.Events,
                new VaccinationDue(d.Record.PetId, d.Record.Id, d.OwnerId, d.Record.NextDueOn.Value, stage, now), d.Record.PetId.ToString(), ct);
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }

    // ---------- Medication doses ----------

    private async Task MedicationDosesAsync(ApplicationDbContext db, DateTime windowStartUtc, DateTime nowUtc, CancellationToken ct)
    {
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, _zone).Date;
        var todayUtc = DateTime.SpecifyKind(localToday, DateTimeKind.Utc);
        var meds = await db.PetMedications.AsNoTracking()
            .Where(m => m.IsActive && m.StartDate <= nowUtc && (m.EndDate == null || m.EndDate >= todayUtc))
            .Select(m => new { m.Id, m.PetId, PetName = m.Pet.Name, m.Pet.OwnerId, m.DrugName, m.Dose, m.DoseTimes })
            .ToListAsync(ct);

        foreach (var m in meds)
        {
            foreach (var t in m.DoseTimes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!TimeOnly.TryParse(t, out var time)) continue;
                var local = DateTime.SpecifyKind(localToday.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
                var scheduledUtc = TimeZoneInfo.ConvertTimeToUtc(local, _zone);
                if (scheduledUtc <= windowStartUtc || scheduledUtc > nowUtc) continue;

                var logged = await db.MedicationDoseLogs.AnyAsync(l => l.MedicationId == m.Id
                    && l.ScheduledFor >= scheduledUtc.AddMinutes(-90) && l.ScheduledFor <= scheduledUtc.AddMinutes(90), ct);
                if (logged) continue;

                foreach (var uid in await RecipientsAsync(db, m.PetId, m.OwnerId, ct, includeViewers: false))
                    db.Notifications.Add(new InAppNotification
                    {
                        UserId = uid,
                        Title = $"Time for {m.PetName}'s {m.DrugName}",
                        Body = m.Dose is null ? "Mark the dose as given in the app." : $"Dose: {m.Dose}. Mark it as given in the app.",
                        Url = $"/pets/{m.PetId}?tab=medications"
                    });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    // ---------- Daily: birthdays & breeding ----------

    private async Task BirthdaysAsync(ApplicationDbContext db, DateOnly today, CancellationToken ct)
    {
        var pets = await db.Pets.AsNoTracking()
            .Where(p => p.BirthDate != null && p.BirthDate.Value.Month == today.Month && p.BirthDate.Value.Day == today.Day)
            .Select(p => new { p.Id, p.Name, p.OwnerId, p.BirthDate })
            .ToListAsync(ct);
        var since = DateTime.UtcNow.AddHours(-20);
        foreach (var p in pets)
        {
            var age = today.Year - p.BirthDate!.Value.Year;
            if (age <= 0) continue;
            var title = $"Happy birthday, {p.Name}! 🎂";
            if (await db.Notifications.AnyAsync(n => n.UserId == p.OwnerId && n.Title == title && n.CreatedAt > since, ct)) continue;
            db.Notifications.Add(new InAppNotification
            {
                UserId = p.OwnerId, Title = title,
                Body = $"{p.Name} turns {age} today. Share a birthday post with the community!",
                Url = $"/pets/{p.Id}"
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task BreedingAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var target = now.Date.AddDays(3);
        var rows = await db.BreedingRecords.AsNoTracking()
            .Where(b => b.OutcomeOn == null && b.ExpectedDueOn != null && b.ExpectedDueOn >= target && b.ExpectedDueOn < target.AddDays(1))
            .Select(b => new { b.OwnerId, b.DamId, DamName = b.Dam.Name, b.ExpectedDueOn })
            .ToListAsync(ct);
        foreach (var b in rows)
            db.Notifications.Add(new InAppNotification
            {
                UserId = b.OwnerId,
                Title = $"{b.DamName} is due in 3 days",
                Body = $"Expected on {b.ExpectedDueOn:dd MMM}. Prepare a clean, quiet space and keep your vet's number handy.",
                Url = "/livestock"
            });
        await db.SaveChangesAsync(ct);
    }

    // ---------- Service bookings ----------

    private static async Task ServiceBookingsAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var windowStart = now.Add(TimeSpan.FromHours(24) - Interval);
        var windowEnd = now.AddHours(24);
        var due = await db.ServiceBookings.AsNoTracking()
            .Where(b => b.Status == AppointmentStatus.Confirmed && b.ScheduledAt >= windowStart && b.ScheduledAt < windowEnd)
            .Select(b => new { b.UserId, b.ScheduledAt, b.ServiceProvider.BusinessName, b.ServiceProvider.ProviderType })
            .ToListAsync(ct);
        foreach (var b in due)
            db.Notifications.Add(new InAppNotification
            {
                UserId = b.UserId,
                Title = $"Reminder: {b.ProviderType.ToString().ToLowerInvariant()} tomorrow",
                Body = $"{b.BusinessName} at {b.ScheduledAt:HH:mm} UTC.",
                Url = "/services/bookings"
            });
        if (due.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static async Task<List<Guid>> RecipientsAsync(ApplicationDbContext db, Guid petId, Guid ownerId, CancellationToken ct,
        bool includeViewers = true)
    {
        var now = DateTime.UtcNow;
        var minRole = includeViewers ? CaretakerRole.Viewer : CaretakerRole.Caretaker;
        var others = await db.PetCaretakers.AsNoTracking()
            .Where(c => c.PetId == petId && c.Role >= minRole && (c.ExpiresAt == null || c.ExpiresAt > now))
            .Select(c => c.UserId).ToListAsync(ct);
        return others.Prepend(ownerId).Distinct().ToList();
    }
}
