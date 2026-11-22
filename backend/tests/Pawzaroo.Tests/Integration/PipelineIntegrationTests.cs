using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Application.Modules.Pets.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Features;
using Pawzaroo.Infrastructure.Modules.Communities;
using Pawzaroo.Infrastructure.Modules.LoyaltyPlus;
using Pawzaroo.Infrastructure.Modules.Pets;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using Xunit;
using StoreEntity = Pawzaroo.Domain.Store.Store;

namespace Pawzaroo.Tests.Integration;

/// <summary>
/// Real-service pipelines against a real Postgres (Testcontainers; needs Docker): checkout pricing (promotion + loyalty +
/// gift card), ledger idempotency, partial-refund allocation vs ledger, SLA monitor idempotency, lost &amp; found fan-out cap
/// and reputation daily caps. Every test seeds its own uniquely-named rows (GUID tags) and asserts only on those rows;
/// the one exception is pricing, which deactivates ALL promotions first because the promotion adjuster is global.
/// </summary>
public class PipelineIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public PipelineIntegrationTests(PostgresFixture fx) => _fx = fx;

    // ------------------------------------------------------------------ plumbing

    private ApplicationDbContext NewDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_fx.Db.Database.GetConnectionString()).UseSnakeCaseNamingConvention().Options);

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(Guid? id, params string[] permissions) { UserId = id; Permissions = permissions; }
        public Guid? UserId { get; }
        public string? Email => null;
        public IReadOnlyCollection<string> Permissions { get; }
        public bool IsAuthenticated => UserId.HasValue;
    }

    /// <summary>Returns completed tasks / default values for every call (notifications, audit, kafka, feed).</summary>
    public class AutoStub : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? m, object?[]? args)
        {
            var rt = m!.ReturnType;
            if (rt == typeof(Task)) return Task.CompletedTask;
            if (rt == typeof(ValueTask)) return default(ValueTask);
            if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = rt.GetGenericArguments()[0];
                var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, new[] { value });
            }
            return rt.IsValueType && rt != typeof(void) ? Activator.CreateInstance(rt) : null;
        }
    }

    private static T Stub<T>() where T : class => DispatchProxy.Create<T, AutoStub>();

    private sealed record Seeded(Guid BuyerId, Guid OwnerId, Guid StoreId, Guid OrderId,
        IReadOnlyList<Guid> ItemIds, IReadOnlyList<Guid> ProductIds);

    /// <summary>One store, one product per line. Line = (qty, unitPrice, commission). Total = lines - discount - credits.</summary>
    private async Task<Seeded> SeedOrderAsync(decimal discount, decimal credits, params (int Qty, decimal Unit, decimal Commission)[] lines)
    {
        await using var db = NewDb();
        var tag = Guid.NewGuid().ToString("N");
        var owner = new User { Email = $"owner-{tag}@test.local", PasswordHash = "x", DisplayName = "owner" };
        var buyer = new User { Email = $"buyer-{tag}@test.local", PasswordHash = "x", DisplayName = "buyer" };
        var store = new StoreEntity { OwnerUserId = owner.Id, Name = $"store-{tag}" };
        db.Users.AddRange(owner, buyer);
        db.Stores.Add(store);

        var subtotal = lines.Sum(l => l.Qty * l.Unit);
        var order = new Order
        {
            UserId = buyer.Id, OrderNumber = "T" + tag[..12], ShippingAddress = "addr",
            Subtotal = subtotal, DiscountAmount = discount, Total = subtotal - discount - credits,
            PaymentStatus = PaymentStatus.Paid
        };
        db.Orders.Add(order);

        var itemIds = new List<Guid>();
        var productIds = new List<Guid>();
        foreach (var l in lines)
        {
            var p = new Product { StoreId = store.Id, Name = "p-" + Guid.NewGuid().ToString("N")[..8], Sku = Guid.NewGuid().ToString("N"), Price = l.Unit, StockQuantity = 10 };
            db.Products.Add(p);
            var it = new OrderItem { OrderId = order.Id, ProductId = p.Id, StoreId = store.Id, Quantity = l.Qty, UnitPrice = l.Unit, Total = l.Qty * l.Unit, CommissionAmount = l.Commission };
            db.OrderItems.Add(it);
            itemIds.Add(it.Id);
            productIds.Add(p.Id);
        }
        await db.SaveChangesAsync();
        return new Seeded(buyer.Id, owner.Id, store.Id, order.Id, itemIds, productIds);
    }

    private LedgerService NewLedger(ApplicationDbContext db) =>
        new(db, new FakeUser(null), new ConfigurationBuilder().Build(), Stub<IAuditLogger>(),
            Stub<INotificationService>(), NullLogger<LedgerService>.Instance);

    // ------------------------------------------------------------------ OrderPricingPipeline

    private static OrderPricingPipeline NewPipeline(ApplicationDbContext db)
    {
        var opt = new LoyaltyPlusOptions();
        var adjusters = new IOrderPricingAdjuster[]
        {
            new PromotionAdjuster(db), new BundleAdjuster(db), new LoyaltyRedemptionAdjuster(db, opt), new GiftCardAdjuster(db)
        };
        var user = new FakeUser(null);
        return new OrderPricingPipeline(db, adjusters,
            new LoyaltyService(db, user, opt, Stub<INotificationService>()), new GiftCardService(db, user));
    }

    private sealed record PricingFixtureData(Seeded Order, Guid PromotionId, Guid GiftCardId, string GiftCode);

    private async Task<PricingFixtureData> SeedPricingAsync()
    {
        var seeded = await SeedOrderAsync(0m, 0m, (2, 500m, 0m));
        await using var db = NewDb();
        await db.Set<Promotion>().ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));   // isolate from other tests

        var promo = new Promotion
        {
            Name = "ten-percent-" + Guid.NewGuid().ToString("N")[..8], Type = PromotionType.PercentOff, Scope = PromotionScope.Storewide,
            Value = 10m, Stackable = true, IsActive = true
        };
        db.Set<Promotion>().Add(promo);
        db.Set<LoyaltyAccount>().Add(new LoyaltyAccount { UserId = seeded.BuyerId, Balance = 5000, LifetimePoints = 5000 });

        var code = "ABCD-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant() + "-JKLM-NPQR";
        var card = new GiftCard
        {
            Code = code, CodeHash = GiftCardMath.Hash(code), InitialAmount = 200m, Balance = 200m, Status = GiftCardStatus.Active
        };
        db.Set<GiftCard>().Add(card);
        await db.SaveChangesAsync();
        return new PricingFixtureData(seeded, promo.Id, card.Id, code);
    }

    private static OrderPricingRequest Request(PricingFixtureData d, bool commit, string? giftCode, int? points) => new(
        d.Order.BuyerId, d.Order.OrderId,
        new[] { new PricingLine(d.Order.ProductIds[0], d.Order.StoreId, null, null, 2, 500m) },
        Subtotal: 1000m, CouponDiscount: 0m, CouponApplied: false,
        ComputeFees: b => (50m, 0m, b + 50m),
        RedeemLoyaltyPoints: points, GiftCardCode: giftCode, MinPayable: 0m, Commit: commit);

    [Fact]
    public async Task Pricing_applies_promotion_then_loyalty_then_gift_card_and_commits_every_side_effect()
    {
        var d = await SeedPricingAsync();
        await using var db = NewDb();

        var result = await NewPipeline(db).ApplyAsync(Request(d, commit: true, d.GiftCode, points: 3000));
        await db.SaveChangesAsync();   // the pipeline stages OrderAppliedAdjustment rows; the caller saves

        // 1000 - 10% promo = 900; + 50 shipping = 950; - 30.00 (3000 pts @ 0.01) - 200 gift card = 720.
        result.MerchandiseDiscount.Should().Be(100m);
        result.ShippingFee.Should().Be(50m);
        result.CreditsApplied.Should().Be(230m);
        result.Total.Should().Be(720m);
        result.Adjustments.Select(a => a.Kind).Should().Equal("promotion", "loyalty", "giftcard");

        await using var check = NewDb();
        (await check.Set<Promotion>().SingleAsync(p => p.Id == d.PromotionId)).UsedCount.Should().Be(1);
        (await check.Set<LoyaltyAccount>().SingleAsync(a => a.UserId == d.Order.BuyerId)).Balance.Should().Be(2000);
        (await check.Set<LoyaltyTransaction>().SingleAsync(t => t.UserId == d.Order.BuyerId && t.OrderId == d.Order.OrderId))
            .Points.Should().Be(-3000);
        var card = await check.Set<GiftCard>().SingleAsync(g => g.Id == d.GiftCardId);
        card.Balance.Should().Be(0m);
        card.Status.Should().Be(GiftCardStatus.Depleted);
        var rows = await check.Set<OrderAppliedAdjustment>().Where(a => a.OrderId == d.Order.OrderId).ToListAsync();
        rows.Select(r => r.Kind).Should().BeEquivalentTo("promotion", "loyalty", "giftcard");
        rows.Where(r => r.Kind is "loyalty" or "giftcard").Sum(r => r.Amount).Should().Be(230m);
    }

    [Fact]
    public async Task Pricing_preview_returns_the_same_numbers_without_mutating_anything()
    {
        var d = await SeedPricingAsync();
        await using var db = NewDb();

        var result = await NewPipeline(db).ApplyAsync(Request(d, commit: false, d.GiftCode, points: 3000));
        await db.SaveChangesAsync();

        result.Total.Should().Be(720m);

        await using var check = NewDb();
        (await check.Set<Promotion>().SingleAsync(p => p.Id == d.PromotionId)).UsedCount.Should().Be(0);
        (await check.Set<LoyaltyAccount>().SingleAsync(a => a.UserId == d.Order.BuyerId)).Balance.Should().Be(5000);
        (await check.Set<GiftCard>().SingleAsync(g => g.Id == d.GiftCardId)).Balance.Should().Be(200m);
        (await check.Set<OrderAppliedAdjustment>().CountAsync(a => a.OrderId == d.Order.OrderId)).Should().Be(0);
    }

    [Fact]
    public async Task Pricing_rejects_an_unknown_gift_card_and_commits_nothing()
    {
        var d = await SeedPricingAsync();
        await using var db = NewDb();

        var act = () => NewPipeline(db).ApplyAsync(Request(d, commit: true, "ZZZZ-ZZZZ-ZZZZ-ZZZZ", points: null));
        await act.Should().ThrowAsync<ValidationException>();

        await using var check = NewDb();
        (await check.Set<OrderAppliedAdjustment>().CountAsync(a => a.OrderId == d.Order.OrderId)).Should().Be(0);
        (await check.Set<GiftCard>().SingleAsync(g => g.Id == d.GiftCardId)).Balance.Should().Be(200m);
    }

    // ------------------------------------------------------------------ ledger idempotency

    [Fact]
    public async Task Recording_order_earnings_twice_yields_one_earning_and_one_commission_per_item()
    {
        // 100 + 300 gross, 40 order discount allocated pro-rata (10 + 30); commission stays on the gross line.
        var s = await SeedOrderAsync(40m, 0m, (1, 100m, 10m), (1, 300m, 30m));

        await using (var db = NewDb()) await NewLedger(db).RecordOrderEarningsAsync(s.OrderId);
        await using (var db = NewDb()) await NewLedger(db).RecordOrderEarningsAsync(s.OrderId);

        await using var check = NewDb();
        var entries = await check.Set<LedgerEntry>().Where(e => e.OrderId == s.OrderId).ToListAsync();
        entries.Should().HaveCount(4);
        entries.GroupBy(e => (e.OrderItemId, e.Type)).Should().OnlyContain(g => g.Count() == 1);
        entries.Where(e => e.Type == LedgerEntryType.Earning).Sum(e => e.Amount).Should().Be(360m);       // 400 - 40
        entries.Where(e => e.Type == LedgerEntryType.Commission).Sum(e => e.Amount).Should().Be(-40m);
        entries.Should().OnlyContain(e => e.StoreId == s.StoreId && e.AvailableAt > DateTime.UtcNow);       // hold applies
    }

    // ------------------------------------------------------------------ partial refund allocation vs ledger

    [Fact]
    public async Task Partial_refund_of_all_items_sums_to_cash_paid_and_ledger_reverses_exactly_what_was_credited()
    {
        // lines 100 (qty 2) + 300, order discount 40, loyalty credit 20 => cash paid 340.
        var s = await SeedOrderAsync(40m, 20m, (2, 50m, 10m), (1, 300m, 30m));
        await using (var db = NewDb())
        {
            db.Set<OrderAppliedAdjustment>().Add(new OrderAppliedAdjustment { OrderId = s.OrderId, Kind = "loyalty", Amount = 20m, Points = 2000 });
            await db.SaveChangesAsync();
            await NewLedger(db).RecordOrderEarningsAsync(s.OrderId);   // store was credited net of the discount (90 / 270)
        }

        PartialRefundResultDto result;
        await using (var db = NewDb())
        {
            var svc = new PartialRefundService(db, new FakeUser(s.BuyerId, Permissions.Orders.Refund), NewLedger(db),
                Stub<IKafkaProducer>(), Stub<IAuditLogger>(), Stub<INotificationService>());
            result = await svc.RefundItemsAsync(s.OrderId, new[]
            {
                new PartialRefundItemInput(s.ItemIds[0], 2),
                new PartialRefundItemInput(s.ItemIds[1], 1)
            }, "damaged");
        }

        // 85 (90 - 5 credit share) + 255 (270 - 15) == cash paid == Order.Total
        result.Lines.Sum(l => l.Amount).Should().Be(340m);
        result.Amount.Should().Be(340m);

        await using var check = NewDb();
        var orderTotal = (await check.Orders.AsNoTracking().SingleAsync(o => o.Id == s.OrderId)).Total;
        (await check.Set<OrderRefund>().Where(r => r.OrderId == s.OrderId).SumAsync(r => r.Amount)).Should().Be(orderTotal);

        // Ledger: refund per item = amount (never more than the credited net), commission reversed pro-rata, never above commission.
        var ledger = await check.Set<LedgerEntry>().Where(e => e.OrderId == s.OrderId).ToListAsync();
        foreach (var itemId in s.ItemIds)
        {
            var mine = ledger.Where(e => e.OrderItemId == itemId).ToList();
            var earning = mine.Single(e => e.Type == LedgerEntryType.Earning).Amount;
            var refund = mine.Single(e => e.Type == LedgerEntryType.Refund).Amount;
            refund.Should().BeLessThan(0m);
            (-refund).Should().BeLessThanOrEqualTo(earning);
            var commission = -mine.Single(e => e.Type == LedgerEntryType.Commission).Amount;
            var reversal = mine.Single(e => e.Type == LedgerEntryType.Reversal).Amount;
            reversal.Should().BeInRange(0.01m, commission);
        }
        ledger.Where(e => e.Type == LedgerEntryType.Refund).Sum(e => -e.Amount).Should().Be(340m);

        // Stock restored by the refunded quantities.
        (await check.Products.AsNoTracking().SingleAsync(p => p.Id == s.ProductIds[0])).StockQuantity.Should().Be(12);
        (await check.Products.AsNoTracking().SingleAsync(p => p.Id == s.ProductIds[1])).StockQuantity.Should().Be(11);

        // An item can be partially refunded only once.
        await using var again = NewDb();
        var svc2 = new PartialRefundService(again, new FakeUser(s.BuyerId, Permissions.Orders.Refund), NewLedger(again),
            Stub<IKafkaProducer>(), Stub<IAuditLogger>(), Stub<INotificationService>());
        var act = () => svc2.RefundItemsAsync(s.OrderId, new[] { new PartialRefundItemInput(s.ItemIds[0], 1) }, "again");
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Partial_refund_requires_the_refund_permission()
    {
        var s = await SeedOrderAsync(0m, 0m, (1, 100m, 10m));
        await using var db = NewDb();
        var svc = new PartialRefundService(db, new FakeUser(s.BuyerId), NewLedger(db),
            Stub<IKafkaProducer>(), Stub<IAuditLogger>(), Stub<INotificationService>());
        var act = () => svc.RefundItemsAsync(s.OrderId, new[] { new PartialRefundItemInput(s.ItemIds[0], 1) }, "x");
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ------------------------------------------------------------------ SLA monitor

    [Fact]
    public async Task Sla_monitor_records_one_breach_row_per_order_and_kind_across_repeated_runs()
    {
        var s = await SeedOrderAsync(0m, 0m, (1, 100m, 0m));
        await using (var db = NewDb())
        {
            // Paid 10 days ago, never shipped or delivered: far past any default ship / deliver target.
            var old = DateTime.UtcNow.AddDays(-10);
            await db.Orders.Where(o => o.Id == s.OrderId).ExecuteUpdateAsync(u => u.SetProperty(o => o.CreatedAt, old));
        }

        await using (var db = NewDb()) await new SlaMonitor(db, Stub<INotificationService>(), NullLogger<SlaMonitor>.Instance).RunAsync();
        await using (var db = NewDb()) await new SlaMonitor(db, Stub<INotificationService>(), NullLogger<SlaMonitor>.Instance).RunAsync();
        await using (var db = NewDb()) await new SlaMonitor(db, Stub<INotificationService>(), NullLogger<SlaMonitor>.Instance).RunAsync();

        await using var check = NewDb();
        var rows = await check.Set<SlaBreach>().Where(b => b.OrderId == s.OrderId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.Kind).Should().BeEquivalentTo(new[] { SlaBreachKind.Ship, SlaBreachKind.Deliver });
        rows.Should().OnlyContain(r => r.StoreId == s.StoreId && r.ResolvedAt == null);
    }

    // ------------------------------------------------------------------ lost & found fan-out cap

    private LostFoundService NewLostFound(ApplicationDbContext db, Guid reporter) =>
        new(db, new FakeUser(reporter), Stub<IFeedService>(), Stub<IKafkaProducer>(), Stub<IAuditLogger>(),
            new ConfigurationBuilder().Build());

    private static CreateLostFoundInput LostReport(string city, double? lat, double? lng) => new(
        LostFoundKind.Lost, null, AnimalType.Dog, null, null, "Brown dog, red collar", null,
        DateTime.UtcNow.AddHours(-2), city, null, lat, lng, ContactPreference.Chat, null, ShareToFeed: false);


    /// <summary>Creates a reporter plus <paramref name="count"/> neighbour users (configured by the callback).</summary>
    private async Task<(Guid Reporter, List<Guid> Neighbours)> SeedNeighboursAsync(int count, Action<int, User> configure)
    {
        await using var db = NewDb();
        var tag = Guid.NewGuid().ToString("N");
        var reporter = new User { Email = $"rep-{tag}@test.local", PasswordHash = "x", DisplayName = "reporter" };
        db.Users.Add(reporter);
        var neighbours = new List<User>();
        for (var i = 0; i < count; i++)
        {
            var u = new User { Email = $"n{i}-{tag}@test.local", PasswordHash = "x", DisplayName = $"n{i}" };
            configure(i, u);
            neighbours.Add(u);
        }
        db.Users.AddRange(neighbours);
        await db.SaveChangesAsync();
        return (reporter.Id, neighbours.Select(n => n.Id).ToList());
    }

    [Fact]
    public async Task Lost_report_city_fan_out_is_capped_at_200_notifications()
    {
        var city = "Capcity" + Guid.NewGuid().ToString("N")[..10];
        var (reporter, neighbours) = await SeedNeighboursAsync(230, (_, u) => u.Location = city);

        Guid reportId;
        await using (var db = NewDb())
            reportId = (await NewLostFound(db, reporter).CreateAsync(LostReport(city, null, null))).Report.Id;

        await using var check = NewDb();
        var url = $"/lost-found/{reportId}";
        var notified = await check.Notifications.Where(n => n.Url == url).Select(n => n.UserId).ToListAsync();
        notified.Should().HaveCount(200);
        notified.Should().OnlyContain(id => neighbours.Contains(id));
        notified.Should().NotContain(reporter);
    }

    [Fact]
    public async Task Lost_report_geofence_fan_out_is_capped_and_ignores_inactive_and_distant_fences()
    {
        var city = "Fencecity" + Guid.NewGuid().ToString("N")[..10];   // matches nobody through the city rule
        const double lat = 23.8103, lng = 90.4125;
        // 230 covering fences + 1 far away + 1 inactive.
        var (reporter, neighbours) = await SeedNeighboursAsync(232, (_, _) => { });
        await using (var db = NewDb())
        {
            for (var i = 0; i < 232; i++)
                db.Set<LostFoundAlertSubscription>().Add(new LostFoundAlertSubscription
                {
                    UserId = neighbours[i], Latitude = i == 230 ? 0.0 : lat, Longitude = i == 230 ? 0.0 : lng,
                    RadiusKm = 50, Active = i != 231
                });
            await db.SaveChangesAsync();
        }

        Guid reportId;
        await using (var db = NewDb())
            reportId = (await NewLostFound(db, reporter).CreateAsync(LostReport(city, lat, lng))).Report.Id;

        await using var check = NewDb();
        var url = $"/lost-found/{reportId}";
        var notified = await check.Notifications.Where(n => n.Url == url).Select(n => n.UserId).ToListAsync();
        notified.Should().HaveCount(200);
        notified.Should().OnlyHaveUniqueItems();
        notified.Should().NotContain(new[] { neighbours[230], neighbours[231], reporter });
    }

    // ------------------------------------------------------------------ reputation caps

    private async Task<Guid> NewUserIdAsync()
    {
        await using var db = NewDb();
        var u = new User { Email = $"rep-{Guid.NewGuid():N}@test.local", PasswordHash = "x", DisplayName = "rep" };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    private async Task<bool> AwardAsync(Guid user, ReputationKind kind, Guid? source = null)
    {
        await using var db = NewDb();
        return await new ReputationService(db, NullLogger<ReputationService>.Instance).AwardAsync(user, null, kind, source ?? Guid.NewGuid());
    }

    [Fact]
    public async Task Reputation_per_kind_daily_cap_and_duplicate_source_are_enforced()
    {
        var user = await NewUserIdAsync();
        var duplicate = Guid.NewGuid();

        // HelpfulAnswer = 5 pts, cap 25 per kind per day -> exactly 5 awards.
        (await AwardAsync(user, ReputationKind.HelpfulAnswer, duplicate)).Should().BeTrue();
        (await AwardAsync(user, ReputationKind.HelpfulAnswer, duplicate)).Should().BeFalse("same (user, kind, source) is idempotent");
        for (var i = 0; i < 4; i++) (await AwardAsync(user, ReputationKind.HelpfulAnswer)).Should().BeTrue();
        (await AwardAsync(user, ReputationKind.HelpfulAnswer)).Should().BeFalse("25/25 points already awarded today for this kind");

        await using var check = NewDb();
        (await check.Set<ReputationEvent>().CountAsync(e => e.UserId == user)).Should().Be(5);
        (await check.Set<UserReputation>().SingleAsync(r => r.UserId == user && r.CommunityId == null)).Total.Should().Be(25);
    }

    [Fact]
    public async Task Reputation_global_daily_cap_blocks_positive_awards_but_never_penalties()
    {
        var user = await NewUserIdAsync();
        for (var i = 0; i < 5; i++) (await AwardAsync(user, ReputationKind.HelpfulAnswer)).Should().BeTrue();   // 25
        for (var i = 0; i < 2; i++) (await AwardAsync(user, ReputationKind.EventHosted)).Should().BeTrue();     // +20 = 45
        for (var i = 0; i < 3; i++) (await AwardAsync(user, ReputationKind.ReportUpheld)).Should().BeTrue();    // +9  = 54
        for (var i = 0; i < 6; i++) (await AwardAsync(user, ReputationKind.PostLiked)).Should().BeTrue();       // +6  = 60 (cap)

        (await AwardAsync(user, ReputationKind.PostLiked)).Should().BeFalse("DailyPositiveCap (60) reached");
        (await AwardAsync(user, ReputationKind.PollCreated)).Should().BeFalse();

        (await AwardAsync(user, ReputationKind.Penalty)).Should().BeTrue("penalties are never capped");

        await using var check = NewDb();
        var row = await check.Set<UserReputation>().SingleAsync(r => r.UserId == user && r.CommunityId == null);
        row.Total.Should().Be(50);   // 60 - 10
    }
}
