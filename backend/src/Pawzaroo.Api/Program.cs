using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Filters;
using Pawzaroo.Api.Hubs;
using Pawzaroo.Api.Middleware;
using Pawzaroo.Api.Services;
using Pawzaroo.Api.Versioning;
using Pawzaroo.Application;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure;
using Pawzaroo.Infrastructure.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Infrastructure.Persistence.Seed;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging.
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// Fail fast on missing/weak secrets. Dev defaults live in appsettings.Development.json;
// production values MUST come from env vars or a secrets manager (Kubernetes Secret,
// Azure Key Vault, AWS Secrets Manager, etc.).
StartupSecretValidator.Validate(builder.Configuration, builder.Environment);

// Application + Infrastructure composition.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiKafkaConsumers();

builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

// Auth.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt section missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true, ValidateAudience = true,
            ValidateIssuerSigningKey = true, ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IUserIdProvider, NameIdentifierUserIdProvider>();

// Rate limiting — global fixed window per user/IP, named policies for auth + writes.
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    opts.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    opts.AddPolicy("writes", ctx =>
        RateLimitPartition.GetTokenBucketLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                QueueLimit = 0
            }));
});

// CORS — whitelist specific headers and methods to avoid the AllowAnyHeader/AllowAnyMethod
// + AllowCredentials combination, which is overly permissive and a CSRF-class risk.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
     .WithHeaders("Content-Type", "Authorization", "X-Correlation-Id", "X-Idempotency-Key")
     .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
     .AllowCredentials()));

builder.Services.AddSignalR();

builder.Services.AddControllers(o =>
    {
        o.Filters.Add<Pawzaroo.Api.Filters.AuditActionFilter>();
        o.Filters.Add<Pawzaroo.Api.Filters.ApiResponseWrappingFilter>();
    })
    // Register controllers in DI so the legacy /api/admin shim can inject the V1
    // controllers it delegates to (AdminController -> {Dashboard,Users}AdminController).
    .AddControllersAsServices()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// FluentValidation: discover validators across the Application assembly; ASP.NET
// model-binding validation also runs.
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssembly(typeof(Pawzaroo.Application.DependencyInjection).Assembly);

// API versioning.
builder.Services.AddPawzarooApiVersioning();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Pawzaroo API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Name = "Authorization"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

// Health checks: Postgres, Redis, Kafka. Forwarded to Prometheus exporter.
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: new[] { "ready" })
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379", name: "redis", tags: new[] { "ready" })
    .AddKafka(opts => { opts.BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092"; },
        name: "kafka", tags: new[] { "ready" })
    .ForwardToPrometheus();

var app = builder.Build();

// Migrate + seed.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseSeeder");
    await DatabaseSeeder.SeedAsync(db, hasher, app.Configuration, seedLogger);
}

app.UseMiddleware<RequestLoggingMiddleware>();   // assigns CorrelationId first
app.UseSerilogRequestLogging();
app.UseMiddleware<Pawzaroo.Api.Middleware.SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

// Force HTTPS outside Development. Skipped locally so HTTP dev proxies keep working.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpMetrics();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
// Runs *after* auth so we know who the user is; blocks suspended accounts
// on every request without burning a DB round-trip (Redis-cached).
app.UseMiddleware<Pawzaroo.Api.Middleware.SuspensionGuardMiddleware>();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");

// /health        — liveness (no checks)
// /health/ready  — readiness probe (postgres + redis + kafka)
// /metrics       — Prometheus scrape endpoint
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapMetrics();

app.Run();

internal class NameIdentifierUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}
