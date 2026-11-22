using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Persistence.Seed;

namespace Pawzaroo.Infrastructure.Persistence;

/// <summary>
/// Startup database bootstrap.
///   --migrate-only : apply EF migrations, then exit (k8s migration Job)
///   --seed-only    : migrate + seed, then exit (k8s seed Job)
///   (no flag)      : migrate + seed only when Database:AutoMigrate is true
///                    (default: true in Development, false elsewhere)
/// Returns true when a CLI mode ran and the host should exit instead of starting.
/// </summary>
public static class DatabaseBootstrapper
{
    public static bool IsAutoMigrateEnabled(IConfiguration config, bool isDevelopment)
        => bool.TryParse(config["Database:AutoMigrate"], out var v) ? v : isDevelopment;

    public static async Task<bool> RunAsync(IServiceProvider services, string[] args, IConfiguration config,
        CancellationToken ct = default)
    {
        var migrateOnly = args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);
        var seedOnly = args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase);

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrapper");
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (migrateOnly)
        {
            logger.LogInformation("[db] --migrate-only: applying migrations");
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("[db] migrations applied");
            return true;
        }

        if (seedOnly)
        {
            logger.LogInformation("[db] --seed-only: migrating and seeding");
            await SeedAsync(sp, config, db, ct);
            logger.LogInformation("[db] seed complete");
            return true;
        }

        var isDev = sp.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;
        if (!IsAutoMigrateEnabled(config, isDev))
        {
            logger.LogInformation("[db] Database:AutoMigrate is off; skipping migrate/seed (run the migration Job)");
            return false;
        }

        // DatabaseSeeder.SeedAsync migrates first, so this covers migrate + seed.
        await SeedAsync(sp, config, db, ct);
        return false;
    }

    private static Task SeedAsync(IServiceProvider sp, IConfiguration config, ApplicationDbContext db, CancellationToken ct)
        => DatabaseSeeder.SeedAsync(db, sp.GetRequiredService<IPasswordHasher>(), config,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder"), ct);
}
