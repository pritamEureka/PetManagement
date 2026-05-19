using Pawzaroo.Application;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Worker.Jobs;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUserService, SystemCurrentUserService>();
builder.Services.AddScoped<INotificationService, NoopNotificationService>();

// Outbox dispatcher: durable producer for events written transactionally.
builder.Services.AddHostedService<OutboxDispatcherJob>();

// Domain consumers — each runs under its own Kafka consumer group.
builder.Services.AddHostedService<NotificationDispatcherJob>();
builder.Services.AddHostedService<OrderEventProjectorJob>();
builder.Services.AddHostedService<FeedDenormalizerJob>();
builder.Services.AddHostedService<AuditLogConsumerJob>();
builder.Services.AddHostedService<AppointmentReminderJob>();

var host = builder.Build();
await host.RunAsync();

file sealed class SystemCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
    public string? Email => null;
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
    public bool IsAuthenticated => false;
}

file sealed class NoopNotificationService : INotificationService
{
    public Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task PushChatMessageToUsersAsync(IEnumerable<Guid> userIds, object payload, CancellationToken ct = default) => Task.CompletedTask;
}
