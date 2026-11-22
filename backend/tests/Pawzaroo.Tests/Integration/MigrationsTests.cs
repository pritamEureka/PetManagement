using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// The shared <see cref="PostgresFixture"/> uses EnsureCreated (fast, model-first). Production uses migrations,
/// so this class boots its OWN empty database and applies the real migration chain with MigrateAsync.
/// Catches: broken/out-of-order migrations, migrations that don't match the model snapshot (EF 9 throws
/// on pending model changes), and non-Postgres-safe SQL.
/// </summary>
public class MigrationsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pawzaroo_migrations")
        .WithUsername("pawzaroo")
        .WithPassword("pawzaroo")
        .Build();

    public Task InitializeAsync() => _pg.StartAsync();
    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private ApplicationDbContext NewContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .UseSnakeCaseNamingConvention()   // match production (DependencyInjection.AddInfrastructure)
            .Options);

    [Fact]
    public async Task migrations_apply_cleanly_to_a_fresh_database()
    {
        await using var db = NewContext();
        (await db.Database.GetAppliedMigrationsAsync()).Should().BeEmpty("the database starts empty");
        db.Database.GetMigrations().Should().NotBeEmpty("the project ships migrations");

        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Database.GetAppliedMigrationsAsync()).Should()
            .BeEquivalentTo(db.Database.GetMigrations());
    }

    [Fact]
    public async Task migrating_twice_is_idempotent()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        await using var second = NewContext();
        var act = () => second.Database.MigrateAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task migrated_schema_is_queryable_through_the_model()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        // Touch a few core tables through EF so a model/schema mismatch (missing table or column) surfaces.
        (await db.Users.CountAsync()).Should().Be(0);
        (await db.Pets.CountAsync()).Should().Be(0);
        (await db.Appointments.CountAsync()).Should().Be(0);
    }
}
