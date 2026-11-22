using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Preventive care-plan digests, every 30 minutes:
///   * flips pending/rescheduled items whose due date has passed to Missed;
///   * "due soon" digest (due today..+2 days) — once per item (LastNotifiedStage = 1);
///   * "missed" digest — once per item (LastNotifiedStage = 2).
/// One notification per recipient per pet per stage. Items with an assignee notify that caretaker only;
/// unassigned items notify the owner plus active co-owners / caretakers. Rescheduling re-arms the stages.
/// Same notification-writing pattern as <see cref="PetCareReminderJob"/>.
/// Registration (Worker/Program.cs): builder.Services.AddHostedService&lt;CarePlanReminderJob&gt;();
/// </summary>
public class CarePlanReminderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private const int DueSoonDays = 2;
    /// <summary>Don't nag about items that were due long ago (imported/seed data).</summary>
    private static readonly TimeSpan StaleMissed = TimeSpan.FromDays(60);

    private readonly IServiceProvider _sp;
    private readonly ILogger<CarePlanReminderJob> _logger;

    public CarePlanReminderJob(IServiceProvider sp, ILogger<CarePlanReminderJob> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await RunOnceAsync(db, DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CarePlanReminderJob iteration failed");
            }
            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }

    internal static async Task RunOnceAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        var today = now.Date;
        var staleBefore = now - StaleMissed;
        var items = db.Set<PetCarePlanItem>();

        // 1. Mark overdue items as Missed.
        var overdue = await items.Where(i => (i.Status == CarePlanItemStatus.Pending || i.Status == CarePlanItemStatus.Rescheduled)
                                             && i.DueDate < today && i.Plan.Status == CarePlanStatus.Active)
            .Take(1000).ToListAsync(ct);
        foreach (var i in overdue) i.Status = CarePlanItemStatus.Missed;
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);

        // 2. Due-soon digest.
        var soon = await items.Where(i => (i.Status == CarePlanItemStatus.Pending || i.Status == CarePlanItemStatus.Rescheduled)
                                          && i.DueDate >= today && i.DueDate < today.AddDays(DueSoonDays + 1)
                                          && i.LastNotifiedStage == null && i.Plan.Status == CarePlanStatus.Active)
            .Take(1000).ToListAsync(ct);
        await DigestAsync(db, soon, 1, "due soon", ct);

        // 3. Missed digest.
        var missed = await items.Where(i => i.Status == CarePlanItemStatus.Missed && i.DueDate > staleBefore
                                            && (i.LastNotifiedStage == null || i.LastNotifiedStage < 2) && i.Plan.Status == CarePlanStatus.Active)
            .Take(1000).ToListAsync(ct);
        await DigestAsync(db, missed, 2, "missed", ct);
    }

    private static async Task DigestAsync(ApplicationDbContext db, List<PetCarePlanItem> batch, int stage, string label, CancellationToken ct)
    {
        if (batch.Count == 0) return;
        var petIds = batch.Select(i => i.PetId).Distinct().ToList();
        var pets = await db.Pets.AsNoTracking().Where(p => petIds.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.OwnerId }).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var caretakers = await db.PetCaretakers.AsNoTracking()
            .Where(c => petIds.Contains(c.PetId) && c.Role >= CaretakerRole.Caretaker && (c.ExpiresAt == null || c.ExpiresAt > now))
            .Select(c => new { c.PetId, c.UserId }).ToListAsync(ct);

        foreach (var pet in pets)
        {
            var petItems = batch.Where(i => i.PetId == pet.Id).OrderBy(i => i.DueDate).ToList();
            var team = caretakers.Where(c => c.PetId == pet.Id).Select(c => c.UserId).Prepend(pet.OwnerId).Distinct().ToList();
            foreach (var uid in team)
            {
                var mine = petItems.Where(i => i.AssignedUserId == null || i.AssignedUserId == uid).ToList();
                if (mine.Count == 0) continue;
                var preview = string.Join(", ", mine.Take(3).Select(i => $"{i.Title} ({i.DueDate:dd MMM})"));
                var more = mine.Count > 3 ? $" and {mine.Count - 3} more" : "";
                db.Notifications.Add(new InAppNotification
                {
                    UserId = uid,
                    Title = $"{pet.Name}: {mine.Count} care item{(mine.Count == 1 ? "" : "s")} {label}",
                    Body = $"{preview}{more}.",
                    Url = $"/pets/{pet.Id}?tab=care-plan"
                });
            }
            foreach (var i in petItems) i.LastNotifiedStage = stage;
        }
        await db.SaveChangesAsync(ct);
    }
}
