using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Common;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// Postgres-specific behaviour the app relies on and that no in-memory provider can verify:
/// JSONB columns / containment operators, ILIKE search translation, and transactional rollback.
/// </summary>
public class PostgresFeatureTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public PostgresFeatureTests(PostgresFixture fx) => _fx = fx;

    private async Task<Guid> SeedOwnerAsync()
    {
        var u = new User { Email = $"o-{Guid.NewGuid():N}@test.local", PasswordHash = "x", DisplayName = "o" };
        _fx.Db.Users.Add(u);
        await _fx.Db.SaveChangesAsync();
        return u.Id;
    }

    // ---------------- JSONB ----------------

    [Fact]
    public async Task model_maps_json_payload_columns_to_jsonb()
    {
        var count = await _fx.Db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE data_type = 'jsonb'")
            .SingleAsync();
        count.Should().BeGreaterThan(0, "the model declares several HasColumnType(\"jsonb\") columns");
    }

    [Fact]
    public async Task jsonb_containment_and_path_extraction_work()
    {
        var table = $"jsonb_probe_{Guid.NewGuid():N}";
        await _fx.Db.Database.ExecuteSqlRawAsync($"CREATE TABLE \"{table}\" (id serial primary key, doc jsonb not null)");
        await _fx.Db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"{table}\" (doc) VALUES " +
            "(jsonb_build_object('species', 'dog', 'tags', jsonb_build_array('vaccinated', 'chipped'))), " +
            "(jsonb_build_object('species', 'cat', 'tags', jsonb_build_array()))");

        var vaccinated = await _fx.Db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"{table}\" WHERE doc @> jsonb_build_object('tags', jsonb_build_array('vaccinated'))")
            .SingleAsync();
        var species = await _fx.Db.Database
            .SqlQueryRaw<string>($"SELECT doc->>'species' AS \"Value\" FROM \"{table}\" WHERE doc ? 'tags' ORDER BY doc->>'species'")
            .ToListAsync();

        vaccinated.Should().Be(1);
        species.Should().BeEquivalentTo(new[] { "cat", "dog" });
    }

    // ---------------- ILIKE search ----------------

    [Fact]
    public async Task ilike_search_is_case_insensitive_and_supports_wildcards()
    {
        var ownerId = await SeedOwnerAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        _fx.Db.Pets.AddRange(
            new Pet { OwnerId = ownerId, Name = $"Mochi-{tag}", AnimalType = AnimalType.Dog },
            new Pet { OwnerId = ownerId, Name = $"MOCHA-{tag}", AnimalType = AnimalType.Cat },
            new Pet { OwnerId = ownerId, Name = $"Pepper-{tag}", AnimalType = AnimalType.Dog });
        await _fx.Db.SaveChangesAsync();

        var hits = await _fx.Db.Pets.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, $"moch%-{tag}"))
            .Select(p => p.Name).ToListAsync();

        hits.Should().BeEquivalentTo(new[] { $"Mochi-{tag}", $"MOCHA-{tag}" });
    }

    [Fact]
    public async Task ilike_treats_escaped_wildcards_literally()
    {
        var ownerId = await SeedOwnerAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        _fx.Db.Pets.AddRange(
            new Pet { OwnerId = ownerId, Name = $"100%-{tag}", AnimalType = AnimalType.Dog },
            new Pet { OwnerId = ownerId, Name = $"1000-{tag}", AnimalType = AnimalType.Dog });
        await _fx.Db.SaveChangesAsync();

        // With an explicit backslash escape character, "\%" matches a literal percent sign.
        var hits = await _fx.Db.Pets.AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Name, $"100\\%-{tag}", "\\"))
            .Select(p => p.Name).ToListAsync();

        hits.Should().Equal($"100%-{tag}");
    }

    // ---------------- Transactions ----------------

    [Fact]
    public async Task rolled_back_transaction_leaves_no_rows()
    {
        var email = $"rb-{Guid.NewGuid():N}@test.local";
        await using (var tx = await _fx.Db.Database.BeginTransactionAsync())
        {
            _fx.Db.Users.Add(new User { Email = email, PasswordHash = "x", DisplayName = "rb" });
            await _fx.Db.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        _fx.Db.ChangeTracker.Clear();

        (await _fx.Db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task unique_email_index_rejects_duplicates()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        _fx.Db.Users.Add(new User { Email = email, PasswordHash = "x", DisplayName = "a" });
        await _fx.Db.SaveChangesAsync();

        _fx.Db.Users.Add(new User { Email = email, PasswordHash = "x", DisplayName = "b" });
        var act = () => _fx.Db.SaveChangesAsync();
        try
        {
            await act.Should().ThrowAsync<DbUpdateException>();
        }
        finally
        {
            _fx.Db.ChangeTracker.Clear();
        }
    }
}
