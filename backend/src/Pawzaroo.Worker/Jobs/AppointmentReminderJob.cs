using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Worker.Jobs;

/// <summary>
/// Every 5 minutes, finds appointments starting in ~24h and publishes reminders.
/// </summary>
public class AppointmentReminderJob : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AppointmentReminderJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeadTime = TimeSpan.FromHours(24);

    public AppointmentReminderJob(IServiceProvider sp, ILogger<AppointmentReminderJob> logger)
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
                var kafka = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

                var windowStart = DateTime.UtcNow.Add(LeadTime - Interval);
                var windowEnd = DateTime.UtcNow.Add(LeadTime);
                var due = await db.Appointments
                    .Where(a => a.Status == AppointmentStatus.Confirmed
                                && a.ScheduledAt >= windowStart && a.ScheduledAt < windowEnd)
                    .Select(a => new { a.Id, a.PatientUserId, a.DoctorId, a.ScheduledAt })
                    .ToListAsync(stoppingToken);

                foreach (var a in due)
                {
                    await kafka.PublishAsync("pawzaroo.notifications",
                        new {
                            type = "appointment.reminder",
                            userId = a.PatientUserId,
                            title = "Upcoming appointment",
                            body = $"Your vet visit is at {a.ScheduledAt:u}",
                            appointmentId = a.Id
                        },
                        a.PatientUserId.ToString(), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AppointmentReminderJob iteration failed");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
        }
    }
}
