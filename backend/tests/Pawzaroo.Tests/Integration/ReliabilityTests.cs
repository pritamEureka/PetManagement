using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Features;
using Pawzaroo.Infrastructure.Features.Reliability;
using Pawzaroo.Infrastructure.Payments;
using Pawzaroo.Infrastructure.Persistence;
using Xunit;

namespace Pawzaroo.Tests.Integration;

/// <summary>Idempotency store, callback duplicate protection and audit immutability against real Postgres.</summary>
public class ReliabilityTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public ReliabilityTests(PostgresFixture fx) => _fx = fx;

    private string Cs => _fx.Db.Database.GetConnectionString()!;

    private DbContextOptions<ApplicationDbContext> Options(bool withInterceptor = false)
    {
        var b = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(Cs).UseSnakeCaseNamingConvention();
        if (withInterceptor) b.AddInterceptors(ReliabilityRegistration.AuditImmutabilityInterceptor);
        return b.Options;
    }

    private IdempotencyStore NewStore()
    {
        var sc = new ServiceCollection();
        sc.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(Cs).UseSnakeCaseNamingConvention());
        return new IdempotencyStore(sc.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    // ---------------- idempotency ----------------

    [Fact]
    public async Task Idempotency_replays_completed_response_in_flight_conflicts_and_mismatch_is_detected()
    {
        var store = NewStore();
        var user = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var first = await store.BeginAsync(user, key, "POST", "/api/v1/orders/checkout", "hashA");
        first.Outcome.Should().Be(IdempotencyBeginOutcome.Proceed);

        // still running
        (await store.BeginAsync(user, key, "POST", "/api/v1/orders/checkout", "hashA"))
            .Outcome.Should().Be(IdempotencyBeginOutcome.InFlight);

        await store.CompleteAsync(first.RecordId, 200, "{\"success\":true}", "application/json");

        var replay = await store.BeginAsync(user, key, "POST", "/api/v1/orders/checkout", "hashA");
        replay.Outcome.Should().Be(IdempotencyBeginOutcome.Replay);
        replay.StatusCode.Should().Be(200);
        replay.Body.Should().Be("{\"success\":true}");
        replay.ContentType.Should().Be("application/json");

        (await store.BeginAsync(user, key, "POST", "/api/v1/orders/checkout", "hashB"))
            .Outcome.Should().Be(IdempotencyBeginOutcome.Mismatch);

        // a different user (or path) with the same key is independent
        (await store.BeginAsync(Guid.NewGuid(), key, "POST", "/api/v1/orders/checkout", "hashA"))
            .Outcome.Should().Be(IdempotencyBeginOutcome.Proceed);
    }

    [Fact]
    public async Task Idempotency_abandoned_key_can_be_retried_and_cleanup_removes_expired()
    {
        var store = NewStore();
        var user = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var first = await store.BeginAsync(user, key, "POST", "/p", "h");
        await store.AbandonAsync(first.RecordId);
        (await store.BeginAsync(user, key, "POST", "/p", "h")).Outcome.Should().Be(IdempotencyBeginOutcome.Proceed);

        await using (var db = new ApplicationDbContext(Options()))
        {
            db.Set<IdempotencyRecord>().Add(new IdempotencyRecord
            {
                Key = "expired-" + key, UserId = user, Method = "POST", Path = "/p", RequestHash = "h",
                Status = IdempotencyStatus.Completed, ExpiresAt = DateTime.UtcNow.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }
        var sc = new ServiceCollection();
        sc.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(Cs).UseSnakeCaseNamingConvention());
        var cleanup = new IdempotencyCleanupService(sc.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        (await cleanup.CleanupExpiredAsync()).Should().BeGreaterThanOrEqualTo(1);
    }

    // ---------------- callback duplicate protection ----------------

    private class StubProxy : DispatchProxy
    {
        public int SucceededCalls;
        protected override object? Invoke(MethodInfo? m, object?[]? args)
        {
            if (m!.Name == nameof(IOrderService.MarkPaymentSucceededAsync)) Interlocked.Increment(ref SucceededCalls);
            if (m.ReturnType == typeof(Task)) return Task.CompletedTask;
            if (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(m.ReturnType.GetGenericArguments()[0])
                    .Invoke(null, new object?[] { m.ReturnType.GetGenericArguments()[0].IsValueType
                        ? Activator.CreateInstance(m.ReturnType.GetGenericArguments()[0]) : null });
            return null;
        }
    }

    private sealed class FakeGateway : IPaymentGateway
    {
        public required PaymentCallbackResult Result { get; init; }
        public bool IsConfigured => true;
        public Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<PaymentCallbackResult?> VerifyCallbackAsync(IDictionary<string, string> f, PaymentCallbackKind k, CancellationToken ct = default)
            => Task.FromResult<PaymentCallbackResult?>(Result);
    }

    private async Task<Guid> NewOrderAsync(decimal total)
    {
        await using var db = new ApplicationDbContext(Options());
        var u = new User { Email = $"cb-{Guid.NewGuid():N}@test.local", PasswordHash = "x", DisplayName = "cb" };
        db.Users.Add(u);
        var o = new Order { UserId = u.Id, OrderNumber = "T" + Guid.NewGuid().ToString("N")[..12], ShippingAddress = "a", Total = total };
        db.Orders.Add(o);
        await db.SaveChangesAsync();
        return o.Id;
    }

    private (PaymentCallbackProcessor Proc, StubProxy Orders, ApplicationDbContext Db) NewProcessor(PaymentCallbackResult r)
    {
        var db = new ApplicationDbContext(Options());
        var orders = DispatchProxy.Create<IOrderService, StubProxy>();
        var proc = new PaymentCallbackProcessor(db, new FakeGateway { Result = r }, orders,
            new PaymentAuditService(db), Options_(new SslCommerzOptions { Currency = "BDT" }));
        return (proc, (StubProxy)(object)orders, db);
    }

    private static IOptions<SslCommerzOptions> Options_(SslCommerzOptions o) => Microsoft.Extensions.Options.Options.Create(o);

    [Fact]
    public async Task Duplicate_success_callback_applies_once_and_replays_result()
    {
        var orderId = await NewOrderAsync(100m);
        var r = new PaymentCallbackResult(PaymentCallbackOutcome.Succeeded, orderId, "bank-" + orderId, 100m, "BDT");
        var (proc, orders, db) = NewProcessor(r);
        await using var _ = db;
        var fields = new Dictionary<string, string> { ["tran_id"] = orderId.ToString(), ["card_no"] = "4111111111111111" };

        var a = await proc.ProcessAsync(fields, PaymentCallbackKind.Ipn, "1.2.3.4");
        var b = await proc.ProcessAsync(fields, PaymentCallbackKind.Ipn, "1.2.3.4");

        a!.Duplicate.Should().BeFalse();
        a.Result.Should().Be("Applied");
        b!.Duplicate.Should().BeTrue();
        b.Result.Should().Be("Applied");
        orders.SucceededCalls.Should().Be(1);

        await using var check = new ApplicationDbContext(Options());
        (await check.Set<PaymentCallbackLog>().CountAsync(l => l.OrderId == orderId)).Should().Be(1);
        var audits = await check.Set<PaymentAuditEntry>().Where(e => e.OrderId == orderId).ToListAsync();
        audits.Should().Contain(e => e.EventType == PaymentAuditEventType.Succeeded);
        audits.Should().Contain(e => e.EventType == PaymentAuditEventType.DuplicateCallback);
        audits.Where(e => e.RawPayloadJson != null).Should().OnlyContain(e => !e.RawPayloadJson!.Contains("card_no"));
    }

    [Fact]
    public async Task Amount_mismatch_never_marks_order_paid()
    {
        var orderId = await NewOrderAsync(100m);
        var r = new PaymentCallbackResult(PaymentCallbackOutcome.Succeeded, orderId, "bank-m-" + orderId, 10m, "BDT");
        var (proc, orders, db) = NewProcessor(r);
        await using var _ = db;

        var res = await proc.ProcessAsync(new Dictionary<string, string> { ["tran_id"] = orderId.ToString() }, PaymentCallbackKind.Success, null);

        res!.Result.Should().Be("AmountMismatch");
        orders.SucceededCalls.Should().Be(0);
    }

    // ---------------- audit immutability ----------------

    [Fact]
    public async Task Payment_audit_entries_reject_update_and_delete()
    {
        await using var db = new ApplicationDbContext(Options(withInterceptor: true));
        var entry = PaymentAuditService.Build(new PaymentAuditRecord(PaymentAuditEventType.Initiated, "sslcommerz", Guid.NewGuid()));
        db.Set<PaymentAuditEntry>().Add(entry);
        await db.SaveChangesAsync(); // append is fine

        entry.Note = "tampered";
        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();

        var loaded = await db.Set<PaymentAuditEntry>().FirstAsync(e => e.Id == entry.Id);
        db.Set<PaymentAuditEntry>().Remove(loaded);
        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<PaymentAuditImmutableException>();
    }
}
