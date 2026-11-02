using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Adoption.Services;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Modules.Adoption;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// "Near me" adoption search against real Postgres — proves the haversine
/// expression translates to SQL and that sort / radius / time filters and
/// offset paging behave. Each test tags its rows with a unique breed and
/// filters on it, so tests sharing the container don't see each other's data.
/// </summary>
public class AdoptionGeoSearchTests : IClassFixture<PostgresFixture>
{
    // Reference points (Bangladesh).
    private static readonly (double Lat, double Lng) Dhaka = (23.8103, 90.4125);
    private static readonly (double Lat, double Lng) Gazipur = (23.9999, 90.4203);       // ~21 km
    private static readonly (double Lat, double Lng) Chattogram = (22.3569, 91.7832);    // ~215 km
    private static readonly (double Lat, double Lng) Sylhet = (24.8949, 91.8687);        // ~195 km

    private readonly PostgresFixture _fx;

    public AdoptionGeoSearchTests(PostgresFixture fx) => _fx = fx;

    private sealed class Anonymous : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public bool IsAuthenticated => false;
    }

    private sealed class NoopKafka : IKafkaProducer
    {
        public Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoopAudit : IAuditLogger
    {
        public Task LogAsync(string action, string entityName, string? entityId, string? module = null,
            object? oldValues = null, object? newValues = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoCache : IRedisCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    }

    private AdoptionListingService Service() =>
        new(_fx.Db, new Anonymous(), new NoopKafka(), new NoopAudit(), new NoCache(), petSharing: null!);

    private async Task<string> SeedAsync()
    {
        var tag = $"geo-{Guid.NewGuid():N}"[..16];
        var owner = new User { Email = $"{tag}@test.local", PasswordHash = "x", DisplayName = "Owner" };
        _fx.Db.Users.Add(owner);

        AdoptionListing L(string title, (double Lat, double Lng)? at, string place, int ageDays, decimal fee = 0) => new()
        {
            OwnerId = owner.Id, Title = title, Breed = tag, AnimalType = AnimalType.Dog, Gender = Gender.Male,
            Status = AdoptionListingStatus.Approved, Location = place, AdoptionFee = fee,
            Latitude = at?.Lat, Longitude = at?.Lng, CreatedAt = DateTime.UtcNow.AddDays(-ageDays)
        };

        var rows = new[]
        {
            L("chattogram", Chattogram, "Chattogram", ageDays: 1, fee: 50),
            L("sylhet", Sylhet, "Sylhet", ageDays: 3, fee: 10),
            L("gazipur", Gazipur, "Gazipur, Dhaka Division", ageDays: 10, fee: 30),
            L("dhaka", Dhaka, "Dhaka", ageDays: 40, fee: 20),
            L("no-point", null, "Somewhere", ageDays: 0),
        };
        var ages = rows.ToDictionary(r => r.Id, r => r.CreatedAt);
        _fx.Db.AdoptionListings.AddRange(rows);
        await _fx.Db.SaveChangesAsync();

        // SaveChanges stamps CreatedAt = now on insert; back-date with a direct update.
        foreach (var (id, createdAt) in ages)
            await _fx.Db.AdoptionListings.Where(l => l.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.CreatedAt, createdAt));
        return tag;
    }

    private static AdoptionListingQuery Q(string tag, string? sort = null, (double Lat, double Lng)? at = null,
        double? radiusKm = null, int? postedWithinDays = null, string? location = null, int pageSize = 20, string? cursor = null) =>
        new(AdoptionListingScope.Public, cursor, pageSize, Breed: tag, Location: location, Sort: sort,
            Latitude: at?.Lat, Longitude: at?.Lng, RadiusKm: radiusKm, PostedWithinDays: postedWithinDays);

    [Fact]
    public async Task with_viewer_location_defaults_to_nearest_first_and_puts_unlocated_last()
    {
        var tag = await SeedAsync();

        var page = await Service().SearchAsync(Q(tag, at: Dhaka));

        page.Items.Select(i => i.Title).Should().ContainInOrder("dhaka", "gazipur", "sylhet", "chattogram", "no-point");
        page.Items[0].DistanceKm.Should().BeLessThan(1);
        page.Items[1].DistanceKm.Should().BeInRange(15, 30);
        page.Items[^1].DistanceKm.Should().BeNull();
    }

    [Fact]
    public async Task radius_keeps_only_listings_inside_the_circle()
    {
        var tag = await SeedAsync();

        var page = await Service().SearchAsync(Q(tag, at: Dhaka, radiusKm: 50));

        page.Items.Select(i => i.Title).Should().BeEquivalentTo(new[] { "dhaka", "gazipur" });
    }

    [Fact]
    public async Task without_location_defaults_to_newest_and_supports_other_sorts()
    {
        var tag = await SeedAsync();
        var svc = Service();

        (await svc.SearchAsync(Q(tag))).Items.Select(i => i.Title)
            .Should().ContainInOrder("no-point", "chattogram", "sylhet", "gazipur", "dhaka");
        (await svc.SearchAsync(Q(tag, sort: "oldest"))).Items[0].Title.Should().Be("dhaka");
        (await svc.SearchAsync(Q(tag, sort: "fee_desc"))).Items[0].Title.Should().Be("chattogram");
        // "nearest" without a point quietly falls back to newest.
        (await svc.SearchAsync(Q(tag, sort: "nearest"))).Items[0].Title.Should().Be("no-point");
    }

    [Fact]
    public async Task time_and_place_filters_combine_with_distance()
    {
        var tag = await SeedAsync();
        var svc = Service();

        (await svc.SearchAsync(Q(tag, at: Dhaka, postedWithinDays: 7))).Items.Select(i => i.Title)
            .Should().ContainInOrder("sylhet", "chattogram", "no-point").And.HaveCount(3);
        // Place filter is case-insensitive substring.
        (await svc.SearchAsync(Q(tag, location: "dhaka"))).Items.Select(i => i.Title)
            .Should().BeEquivalentTo(new[] { "dhaka", "gazipur" });
    }

    [Fact]
    public async Task nearest_pages_with_offset_cursor_without_gaps_or_repeats()
    {
        var tag = await SeedAsync();
        var svc = Service();

        var first = await svc.SearchAsync(Q(tag, at: Dhaka, pageSize: 2));
        var second = await svc.SearchAsync(Q(tag, at: Dhaka, pageSize: 2, cursor: first.NextCursor));
        var third = await svc.SearchAsync(Q(tag, at: Dhaka, pageSize: 2, cursor: second.NextCursor));

        first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Title)
            .Should().Equal("dhaka", "gazipur", "sylhet", "chattogram", "no-point");
        third.NextCursor.Should().BeNull();
    }

    [Fact]
    public void approximate_rounds_to_about_a_kilometre_and_rejects_partial_points()
    {
        GeoSearch.Approximate(23.810349, 90.412521).Should().Be((23.81, 90.41));
        GeoSearch.Approximate(23.8, null).Should().Be(((double?)null, (double?)null));
        GeoSearch.Approximate(123, 90).Should().Be(((double?)null, (double?)null));
        GeoSearch.DistanceKm(Dhaka.Lat, Dhaka.Lng, Chattogram.Lat, Chattogram.Lng).Should().BeInRange(200, 230);
    }
}
