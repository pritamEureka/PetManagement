using Pawzaroo.Application;
using Pawzaroo.Infrastructure;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Worker.Jobs;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(cfg => cfg.WriteTo.Console());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
