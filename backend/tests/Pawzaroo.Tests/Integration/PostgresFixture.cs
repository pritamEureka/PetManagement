using Microsoft.EntityFrameworkCore;
using Pawzaroo.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Pawzaroo.Tests.Integration;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("pawzaroo_test")
        .WithUsername("pawzaroo")
        .WithPassword("pawzaroo")
        .Build();

    public ApplicationDbContext Db { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        Db = new ApplicationDbContext(opts);
        await Db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}
