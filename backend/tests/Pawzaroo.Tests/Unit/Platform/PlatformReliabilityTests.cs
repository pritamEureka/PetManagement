using System.Collections.Concurrent;
using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Messaging;
using Pawzaroo.Infrastructure.Observability;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using Xunit;

namespace Pawzaroo.Tests.Unit.Platform;

public class BackoffTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(5, 32)]
    public void Outbox_backoff_doubles_without_jitter(int attempts, int seconds)
        => OutboxBackoff.Delay(attempts, 0.5).TotalSeconds.Should().BeApproximately(seconds, 0.001);

    [Fact]
    public void Outbox_backoff_is_capped_and_overflow_safe()
    {
        OutboxBackoff.Delay(12, 1.0).Should().BeLessThanOrEqualTo(OutboxBackoff.Max);
        OutboxBackoff.Delay(int.MaxValue, 1.0).Should().BeLessThanOrEqualTo(OutboxBackoff.Max);
        OutboxBackoff.Delay(0, 0.5).TotalSeconds.Should().BeApproximately(2, 0.001);
    }

    [Fact]
    public void Outbox_backoff_jitter_stays_within_bounds()
    {
        var lo = OutboxBackoff.Delay(3, 0).TotalSeconds;
        var hi = OutboxBackoff.Delay(3, 1).TotalSeconds;
        lo.Should().BeApproximately(8 * 0.8, 0.001);
        hi.Should().BeApproximately(8 * 1.2, 0.001);
    }

    [Fact]
    public void NextAttemptAt_is_in_the_future()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        OutboxBackoff.NextAttemptAt(now, 2, 0.5).Should().Be(now.AddSeconds(4));
    }

    [Fact]
    public void Consumer_backoff_grows_and_caps()
    {
        ConsumerBackoff.Delay(500, 0).TotalMilliseconds.Should().Be(500);
        ConsumerBackoff.Delay(500, 2).TotalMilliseconds.Should().Be(2000);
        ConsumerBackoff.Delay(500, 2, 100).TotalMilliseconds.Should().Be(2100);
        ConsumerBackoff.Delay(500, 40).Should().Be(ConsumerBackoff.Cap);
    }
}

public class FailureClassificationTests
{
    [Fact]
    public void Permanent_consumer_failures()
    {
        FailureClassifier.ClassifyConsumerFailure(new JsonException("bad")).Should().Be(FailureKind.Permanent);
        FailureClassifier.ClassifyConsumerFailure(new FormatException()).Should().Be(FailureKind.Permanent);
        FailureClassifier.ClassifyConsumerFailure(new ArgumentNullException("x")).Should().Be(FailureKind.Permanent);
        FailureClassifier.ClassifyConsumerFailure(new NotFoundException("Order", 1)).Should().Be(FailureKind.Permanent);
        FailureClassifier.ClassifyConsumerFailure(new KeyNotFoundException()).Should().Be(FailureKind.Permanent);
    }

    [Fact]
    public void Wrapped_permanent_failure_is_still_permanent()
        => FailureClassifier.ClassifyConsumerFailure(new InvalidOperationException("outer", new JsonException("inner")))
            .Should().Be(FailureKind.Permanent);

    [Fact]
    public void Transient_and_unknown_consumer_failures_are_retried()
    {
        FailureClassifier.ClassifyConsumerFailure(new TimeoutException()).Should().Be(FailureKind.Transient);
        FailureClassifier.ClassifyConsumerFailure(new IOException()).Should().Be(FailureKind.Transient);
        FailureClassifier.ClassifyConsumerFailure(new InvalidOperationException("who knows")).Should().Be(FailureKind.Transient);
    }

    [Fact]
    public void Producer_failures_split_by_error_code()
    {
        var tooBig = new ProduceException<string, string>(new Error(ErrorCode.MsgSizeTooLarge), null!);
        var timedOut = new ProduceException<string, string>(new Error(ErrorCode.Local_MsgTimedOut), null!);
        FailureClassifier.ClassifyProducerFailure(tooBig).Should().Be(FailureKind.Permanent);
        FailureClassifier.ClassifyProducerFailure(timedOut).Should().Be(FailureKind.Transient);
        FailureClassifier.ClassifyProducerFailure(new TimeoutException()).Should().Be(FailureKind.Transient);
    }
}

public class BuildInfoTests
{
    [Theory]
    [InlineData("1.2.3+abcdef", null, "1.2.3")]
    [InlineData("1.2.3", null, "1.2.3")]
    [InlineData("1.2.3+abc", "9.9.9", "9.9.9")]
    [InlineData(null, null, "0.0.0")]
    [InlineData("  ", " ", "0.0.0")]
    public void Version_resolution(string? informational, string? env, string expected)
        => BuildInfo.ResolveVersion(informational, env).Should().Be(expected);

    [Theory]
    [InlineData(null, "dev")]
    [InlineData("", "dev")]
    [InlineData(" abc123 ", "abc123")]
    public void GitSha_falls_back_to_dev(string? env, string expected)
        => BuildInfo.ResolveGitSha(env).Should().Be(expected);

    [Fact]
    public void Static_values_are_never_empty()
    {
        BuildInfo.Version.Should().NotBeNullOrWhiteSpace();
        BuildInfo.GitSha.Should().NotBeNullOrWhiteSpace();
    }
}

public class SingleFlightTests
{
    private sealed class FakeBackend : ISingleFlightBackend
    {
        private readonly ConcurrentDictionary<string, string> _store = new();
        private readonly ConcurrentDictionary<string, string> _locks = new();
        public bool Down { get; set; }

        public Task<(bool Hit, T? Value)> TryGetAsync<T>(string key, CancellationToken ct)
        {
            if (Down) throw new InvalidOperationException("down");
            return Task.FromResult(_store.TryGetValue(key, out var json)
                ? (true, JsonSerializer.Deserialize<T>(json))
                : (false, default(T)));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
        {
            _store[key] = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task<string?> TryLockAsync(string key, TimeSpan ttl, CancellationToken ct)
        {
            if (Down) throw new InvalidOperationException("down");
            var token = Guid.NewGuid().ToString("N");
            return Task.FromResult(_locks.TryAdd(key, token) ? token : null);
        }

        public Task UnlockAsync(string key, string token, CancellationToken ct)
        {
            _locks.TryRemove(new KeyValuePair<string, string>(key, token));
            return Task.CompletedTask;
        }

        public void ForceLock(string key) => _locks[key] = "peer";
        public void Put(string key, string json) => _store[key] = json;
    }

    [Fact]
    public async Task Concurrent_misses_run_the_loader_once()
    {
        var sf = new SingleFlight(new FakeBackend());
        var calls = 0;
        var key = "sf-" + Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ =>
            sf.GetOrCreateAsync<string>(key, async ct =>
            {
                Interlocked.Increment(ref calls);
                await Task.Delay(100, ct);
                return "value";
            }, TimeSpan.FromMinutes(1))));

        calls.Should().Be(1);
        results.Should().OnlyContain(r => r == "value");
    }

    [Fact]
    public async Task Hit_skips_the_loader()
    {
        var backend = new FakeBackend();
        var key = "sf-" + Guid.NewGuid();
        backend.Put(key, "\"cached\"");
        var r = await new SingleFlight(backend).GetOrCreateAsync<string>(key, _ => throw new Exception("no"), TimeSpan.FromMinutes(1));
        r.Should().Be("cached");
    }

    [Fact]
    public async Task Waits_for_peer_node_then_reads_its_value()
    {
        var backend = new FakeBackend();
        var key = "sf-" + Guid.NewGuid();
        backend.ForceLock(key);
        var sf = new SingleFlight(backend, peerWait: TimeSpan.FromSeconds(2), poll: TimeSpan.FromMilliseconds(10));

        var task = sf.GetOrCreateAsync<string>(key, _ => throw new Exception("should not load"), TimeSpan.FromMinutes(1));
        await Task.Delay(100);
        backend.Put(key, "\"from-peer\"");
        (await task).Should().Be("from-peer");
    }

    [Fact]
    public async Task Fails_open_when_peer_never_fills_the_cache()
    {
        var backend = new FakeBackend();
        var key = "sf-" + Guid.NewGuid();
        backend.ForceLock(key);
        var sf = new SingleFlight(backend, peerWait: TimeSpan.FromMilliseconds(100), poll: TimeSpan.FromMilliseconds(10));
        (await sf.GetOrCreateAsync<string>(key, _ => Task.FromResult<string?>("loaded"), TimeSpan.FromMinutes(1))).Should().Be("loaded");
    }

    [Fact]
    public async Task Fails_open_when_backend_is_down()
    {
        var sf = new SingleFlight(new FakeBackend { Down = true });
        (await sf.GetOrCreateAsync<string>("sf-" + Guid.NewGuid(), _ => Task.FromResult<string?>("loaded"), TimeSpan.FromMinutes(1)))
            .Should().Be("loaded");
    }

    [Fact]
    public async Task Null_results_are_not_cached()
    {
        var sf = new SingleFlight(new FakeBackend());
        var key = "sf-" + Guid.NewGuid();
        var calls = 0;
        Task<string?> Load(CancellationToken _) { calls++; return Task.FromResult<string?>(null); }
        await sf.GetOrCreateAsync<string>(key, Load, TimeSpan.FromMinutes(1));
        await sf.GetOrCreateAsync<string>(key, Load, TimeSpan.FromMinutes(1));
        calls.Should().Be(2);
    }
}

public class DatabaseBootstrapperTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("true", false, true)]
    [InlineData("false", true, false)]
    [InlineData("garbage", true, true)]
    public void AutoMigrate_defaults_follow_environment(string? cfg, bool isDev, bool expected)
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(cfg is null ? new Dictionary<string, string?>() : new() { ["Database:AutoMigrate"] = cfg })
            .Build();
        DatabaseBootstrapper.IsAutoMigrateEnabled(config, isDev).Should().Be(expected);
    }
}
