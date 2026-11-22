using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Modules.Marketplace;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.LoyaltyPlus;

internal static class PlusAccess
{
    public static bool IsAdmin(ICurrentUserService c) => c.Permissions.Contains(Permissions.Settings.Edit);

    public static ValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = new[] { message } });
}

public class LoyaltyService : ILoyaltyService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly LoyaltyPlusOptions _opt;
    private readonly INotificationService _notify;

    public LoyaltyService(ApplicationDbContext db, ICurrentUserService current, LoyaltyPlusOptions opt, INotificationService notify)
    {
        _db = db; _current = current; _opt = opt; _notify = notify;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    // ---- account ----------------------------------------------------------------------

    internal async Task<LoyaltyAccount> EnsureAccountAsync(Guid userId, CancellationToken ct)
    {
        var acct = await _db.Set<LoyaltyAccount>().FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (acct is not null) return acct;
        acct = new LoyaltyAccount { UserId = userId };
        _db.Set<LoyaltyAccount>().Add(acct);
        try { await _db.SaveChangesAsync(ct); return acct; }
        catch (DbUpdateException)
        {
            _db.Entry(acct).State = EntityState.Detached;
            return await _db.Set<LoyaltyAccount>().FirstAsync(a => a.UserId == userId, ct);
        }
    }

    private async Task ApplyDeltaAsync(Guid userId, int balanceDelta, int lifetimeDelta, CancellationToken ct)
    {
        await _db.Set<LoyaltyAccount>().Where(a => a.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Balance, a => a.Balance + balanceDelta)
                .SetProperty(a => a.LifetimePoints, a => a.LifetimePoints + lifetimeDelta)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);
        var lifetime = await _db.Set<LoyaltyAccount>().AsNoTracking().Where(a => a.UserId == userId)
            .Select(a => a.LifetimePoints).FirstOrDefaultAsync(ct);
        var tier = LoyaltyRules.TierFor(lifetime);
        await _db.Set<LoyaltyAccount>().Where(a => a.UserId == userId && a.Tier != tier)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Tier, tier), ct);
    }

    private async Task<bool> TryAddTransactionAsync(LoyaltyTransaction tx, CancellationToken ct)
    {
        _db.Set<LoyaltyTransaction>().Add(tx);
        try { await _db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException)
        {
            _db.Entry(tx).State = EntityState.Detached; // unique (order,type) => already processed
            return false;
        }
    }

    // ---- earn / reverse (order hooks) -------------------------------------------------

    /// <summary>Idempotent: one Earn row per order (unique index), plus the referral reward on the referred user's first delivered order.</summary>
    public async Task EarnForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new { o.UserId, o.Total, o.ShippingFee, o.Tax, o.Status, o.PaymentStatus, o.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (order is null || order.Status != OrderStatus.Delivered || order.PaymentStatus == PaymentStatus.Refunded) return;

        var already = await _db.Set<LoyaltyTransaction>().AnyAsync(t => t.OrderId == orderId && t.Type == LoyaltyTransactionType.Earn, ct);
        if (!already)
        {
            var acct = await EnsureAccountAsync(order.UserId, ct);
            var basis = Math.Max(0m, order.Total - order.ShippingFee - order.Tax);
            var pts = LoyaltyRules.PointsForOrder(basis, _opt.PointsPerCurrencyUnit, acct.Tier);
            var added = await TryAddTransactionAsync(new LoyaltyTransaction
            {
                UserId = order.UserId, Type = LoyaltyTransactionType.Earn, Points = pts, OrderId = orderId,
                Note = pts > 0 ? "Points earned on delivered order" : "No points (zero basis)",
                ExpiresAt = _opt.PointsExpiryDays > 0 ? DateTime.UtcNow.AddDays(_opt.PointsExpiryDays) : null
            }, ct);
            if (added && pts > 0) await ApplyDeltaAsync(order.UserId, pts, pts, ct);
        }

        await GrantReferralRewardAsync(order.UserId, orderId, order.CreatedAt, ct);
    }

    private async Task GrantReferralRewardAsync(Guid referredUserId, Guid orderId, DateTime orderCreatedAt, CancellationToken ct)
    {
        var red = await _db.Set<ReferralRedemption>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.ReferredUserId == referredUserId && r.RewardedAt == null, ct);
        if (red is null) return;
        var delivered = await _db.Orders.AsNoTracking().CountAsync(o => o.UserId == referredUserId
            && o.Status == OrderStatus.Delivered && o.CreatedAt <= orderCreatedAt, ct);
        if (!ReferralRules.ShouldReward(delivered, red.RewardedAt)) return;

        var now = DateTime.UtcNow;
        var claimed = await _db.Set<ReferralRedemption>().Where(r => r.Id == red.Id && r.RewardedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RewardedAt, (DateTime?)now), ct);
        if (claimed == 0) return;

        var exp = _opt.PointsExpiryDays > 0 ? now.AddDays(_opt.PointsExpiryDays) : (DateTime?)null;
        foreach (var (uid, pts, note) in new[]
        {
            (red.ReferrerUserId, _opt.ReferrerRewardPoints, "Referral reward: your friend's first order was delivered"),
            (red.ReferredUserId, _opt.ReferredBonusPoints, "Welcome bonus for joining with a referral code")
        })
        {
            if (pts <= 0) continue;
            await EnsureAccountAsync(uid, ct);
            _db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
            { UserId = uid, Type = LoyaltyTransactionType.Earn, Points = pts, Note = note, ExpiresAt = exp });
            await _db.SaveChangesAsync(ct);
            await ApplyDeltaAsync(uid, pts, pts, ct);
            await _notify.NotifyUserAsync(uid, "Referral reward", $"You earned {pts} loyalty points.", new { orderId, points = pts }, ct);
        }
    }

    /// <summary>
    /// Reverses earned points (proportionally on partial refunds, once per order) and, on a full close, restores
    /// redeemed points. Reversal is capped at the current balance so balances never go negative.
    /// </summary>
    public async Task ReverseForOrderAsync(Guid orderId, decimal? refundAmount, decimal orderTotal, bool full, CancellationToken ct = default)
    {
        var rows = await _db.Set<LoyaltyTransaction>().AsNoTracking().Where(t => t.OrderId == orderId).ToListAsync(ct);
        var earn = rows.FirstOrDefault(t => t.Type == LoyaltyTransactionType.Earn);
        if (earn is { Points: > 0 } && rows.All(t => t.Type != LoyaltyTransactionType.Reverse))
        {
            var fraction = full || orderTotal <= 0m ? 1m : Math.Min(1m, (refundAmount ?? orderTotal) / orderTotal);
            var want = (int)Math.Floor(earn.Points * fraction);
            var balance = await _db.Set<LoyaltyAccount>().AsNoTracking().Where(a => a.UserId == earn.UserId)
                .Select(a => a.Balance).FirstOrDefaultAsync(ct);
            var take = Math.Min(want, Math.Max(0, balance));
            if (take > 0 && await TryAddTransactionAsync(new LoyaltyTransaction
            {
                UserId = earn.UserId, Type = LoyaltyTransactionType.Reverse, Points = -take, OrderId = orderId,
                Note = "Points reversed (order cancelled/refunded)"
            }, ct))
                await ApplyDeltaAsync(earn.UserId, -take, -take, ct);
        }

        var redeem = rows.FirstOrDefault(t => t.Type == LoyaltyTransactionType.Redeem);
        if (full && redeem is not null && rows.All(t => t.Type != LoyaltyTransactionType.Adjust))
        {
            var pts = -redeem.Points;
            if (pts > 0 && await TryAddTransactionAsync(new LoyaltyTransaction
            {
                UserId = redeem.UserId, Type = LoyaltyTransactionType.Adjust, Points = pts, OrderId = orderId,
                Note = "Redeemed points restored (order cancelled/refunded)"
            }, ct))
                await ApplyDeltaAsync(redeem.UserId, pts, 0, ct);
        }
    }

    // ---- user-facing ----------------------------------------------------------------------

    public async Task<LoyaltyAccountDto> GetMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var acct = await EnsureAccountAsync(uid, ct);
        var code = await GetOrCreateReferralCodeAsync(uid, ct);
        var refs = await _db.Set<ReferralRedemption>().AsNoTracking().Where(r => r.ReferrerUserId == uid)
            .Select(r => r.RewardedAt != null).ToListAsync(ct);
        LoyaltyTier? next = acct.Tier switch
        {
            LoyaltyTier.Bronze => LoyaltyTier.Silver, LoyaltyTier.Silver => LoyaltyTier.Gold,
            LoyaltyTier.Gold => LoyaltyTier.Platinum, _ => null
        };
        var threshold = next switch
        {
            LoyaltyTier.Silver => 1000, LoyaltyTier.Gold => 5000, LoyaltyTier.Platinum => 15000, _ => 0
        };
        return new LoyaltyAccountDto(acct.Balance, acct.LifetimePoints, acct.Tier, next,
            next is null ? 0 : Math.Max(0, threshold - acct.LifetimePoints),
            _opt.ValuePerPoint, LoyaltyRules.PointsValue(acct.Balance, _opt.ValuePerPoint),
            code, refs.Count, refs.Count(x => x));
    }

    public async Task<PageResult<LoyaltyTransactionDto>> HistoryAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var uid = Uid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Set<LoyaltyTransaction>().AsNoTracking().Where(t => t.UserId == uid);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(t => t.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new LoyaltyTransactionDto(t.Id, t.Type, t.Points, t.OrderId, t.Note, t.CreatedAt, t.ExpiresAt))
            .ToListAsync(ct);
        return new PageResult<LoyaltyTransactionDto>(items, total, page, pageSize);
    }

    public async Task<LoyaltyRedeemPreviewDto> PreviewRedeemAsync(decimal payable, CancellationToken ct = default)
    {
        var uid = Uid();
        var balance = await _db.Set<LoyaltyAccount>().AsNoTracking().Where(a => a.UserId == uid)
            .Select(a => a.Balance).FirstOrDefaultAsync(ct);
        var max = LoyaltyRules.MaxRedeemablePoints(balance, payable, _opt.ValuePerPoint, _opt.MaxRedeemPercent);
        return new LoyaltyRedeemPreviewDto(max, LoyaltyRules.PointsValue(max, _opt.ValuePerPoint), balance, _opt.ValuePerPoint);
    }

    private async Task<string> GetOrCreateReferralCodeAsync(Guid uid, CancellationToken ct)
    {
        var existing = await _db.Set<ReferralCode>().AsNoTracking().Where(c => c.UserId == uid).Select(c => c.Code).FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var code = new string(Enumerable.Range(0, 8)
                .Select(_ => CodeAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
            var row = new ReferralCode { UserId = uid, Code = code };
            _db.Set<ReferralCode>().Add(row);
            try { await _db.SaveChangesAsync(ct); return code; }
            catch (DbUpdateException)
            {
                _db.Entry(row).State = EntityState.Detached;
                var again = await _db.Set<ReferralCode>().AsNoTracking().Where(c => c.UserId == uid).Select(c => c.Code).FirstOrDefaultAsync(ct);
                if (again is not null) return again;
            }
        }
        throw new ConflictException("Could not allocate a referral code; try again.");
    }

    public async Task RedeemReferralCodeAsync(string code, CancellationToken ct = default)
    {
        var uid = Uid();
        var norm = ReferralRules.NormalizeCode(code);
        var owner = await _db.Set<ReferralCode>().AsNoTracking().FirstOrDefaultAsync(c => c.Code == norm, ct)
                    ?? throw PlusAccess.Invalid("code", "Unknown referral code.");
        var alreadyReferred = await _db.Set<ReferralRedemption>().AnyAsync(r => r.ReferredUserId == uid, ct);
        var hasOrders = await _db.Orders.AnyAsync(o => o.UserId == uid && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Denied, ct);
        if (!ReferralRules.CanRedeem(owner.UserId, uid, alreadyReferred, hasOrders))
            throw PlusAccess.Invalid("code", "Referral codes can only be used once, by new customers, and not on your own code.");

        _db.Set<ReferralRedemption>().Add(new ReferralRedemption { Code = norm, ReferrerUserId = owner.UserId, ReferredUserId = uid });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A referral code was already applied to your account."); }
    }

    public async Task AdjustAsync(AdjustLoyaltyInput input, CancellationToken ct = default)
    {
        if (!PlusAccess.IsAdmin(_current)) throw new ForbiddenException();
        if (input.Points == 0) throw PlusAccess.Invalid("points", "Points must be non-zero.");
        var acct = await EnsureAccountAsync(input.UserId, ct);
        var delta = input.Points < 0 ? -Math.Min(-input.Points, acct.Balance) : input.Points;
        if (delta == 0) return;
        _db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
        {
            UserId = input.UserId, Type = LoyaltyTransactionType.Adjust, Points = delta,
            Note = string.IsNullOrWhiteSpace(input.Note) ? "Manual adjustment" : input.Note!.Trim()
        });
        await _db.SaveChangesAsync(ct);
        await ApplyDeltaAsync(input.UserId, delta, Math.Max(0, delta), ct);
    }

    // ---- worker ------------------------------------------------------------------------

    public async Task<int> ExpirePointsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Catch-up: delivered orders (any delivery path) that never earned points.
        var since = now.AddDays(-60);
        var missing = await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered && o.PaymentStatus != PaymentStatus.Refunded && o.CreatedAt >= since
                        && !_db.Set<LoyaltyTransaction>().Any(t => t.OrderId == o.Id && t.Type == LoyaltyTransactionType.Earn))
            .Select(o => o.Id).Take(200).ToListAsync(ct);
        foreach (var id in missing)
        {
            try { await EarnForOrderAsync(id, ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* next run retries */ }
        }

        var userIds = await _db.Set<LoyaltyTransaction>().AsNoTracking()
            .Where(t => t.Type == LoyaltyTransactionType.Earn && t.ExpiresAt != null && t.ExpiresAt <= now && t.Points > 0
                        && _db.Set<LoyaltyAccount>().Any(a => a.UserId == t.UserId && a.Balance > 0))
            .Select(t => t.UserId).Distinct().Take(500).ToListAsync(ct);

        var touched = 0;
        foreach (var uid in userIds)
        {
            var txs = await _db.Set<LoyaltyTransaction>().AsNoTracking().Where(t => t.UserId == uid).ToListAsync(ct);
            var balance = await _db.Set<LoyaltyAccount>().AsNoTracking().Where(a => a.UserId == uid).Select(a => a.Balance).FirstOrDefaultAsync(ct);
            var lots = txs.Where(t => t.Type == LoyaltyTransactionType.Earn && t.Points > 0)
                .Select(t => new LoyaltyRules.EarnLot(t.Points, t.ExpiresAt));
            var consumed = -txs.Where(t => t.Type != LoyaltyTransactionType.Earn).Sum(t => t.Points);
            var expire = LoyaltyRules.PointsToExpire(lots, consumed, balance, now);
            if (expire <= 0) continue;

            _db.Set<LoyaltyTransaction>().Add(new LoyaltyTransaction
            { UserId = uid, Type = LoyaltyTransactionType.Expire, Points = -expire, Note = "Points expired" });
            await _db.SaveChangesAsync(ct);
            await ApplyDeltaAsync(uid, -expire, 0, ct);
            await _notify.NotifyUserAsync(uid, "Loyalty points expired", $"{expire} points expired.", new { points = expire }, ct);
            touched++;
        }
        return touched;
    }
}

public class GiftCardService : IGiftCardService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GiftCardService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private void EnsureAdmin() { if (!PlusAccess.IsAdmin(_current)) throw new ForbiddenException(); }

    public async Task<GiftCardDto> IssueAsync(IssueGiftCardInput input, CancellationToken ct = default)
    {
        EnsureAdmin();
        if (input.Amount <= 0m || input.Amount > 100000m) throw PlusAccess.Invalid("amount", "Amount must be between 0 and 100000.");
        if (input.ExpiresAt is { } e && e <= DateTime.UtcNow) throw PlusAccess.Invalid("expiresAt", "Expiry must be in the future.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = GiftCardMath.GenerateCode();
            var card = new GiftCard
            {
                Code = code, CodeHash = GiftCardMath.Hash(code),
                InitialAmount = Math.Round(input.Amount, 2), Balance = Math.Round(input.Amount, 2),
                Currency = string.IsNullOrWhiteSpace(input.Currency) ? "BDT" : input.Currency!.Trim().ToUpperInvariant(),
                RecipientEmail = input.RecipientEmail?.Trim(), Message = input.Message?.Trim(),
                ExpiresAt = input.ExpiresAt, Status = GiftCardStatus.Active, CreatedBy = _current.UserId
            };
            _db.Set<GiftCard>().Add(card);
            _db.Set<GiftCardTransaction>().Add(new GiftCardTransaction
            { GiftCardId = card.Id, Type = GiftCardTransactionType.Issue, Amount = card.InitialAmount, Note = "Issued by admin" });
            try { await _db.SaveChangesAsync(ct); return ToDto(card); }
            catch (DbUpdateException)
            {
                foreach (var en in _db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList()) en.State = EntityState.Detached;
            }
        }
        throw new ConflictException("Could not generate a unique gift card code.");
    }

    public async Task<IReadOnlyList<GiftCardDto>> ListAsync(int take, CancellationToken ct = default)
    {
        EnsureAdmin();
        var rows = await _db.Set<GiftCard>().AsNoTracking().OrderByDescending(g => g.CreatedAt)
            .Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return rows.Select(g => ToDto(g)).ToList();
    }

    public async Task CancelAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();
        var rows = await _db.Set<GiftCard>().Where(g => g.Id == id && g.Status != GiftCardStatus.Cancelled)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GiftCardStatus.Cancelled), ct);
        if (rows == 0 && !await _db.Set<GiftCard>().AnyAsync(g => g.Id == id, ct)) throw new NotFoundException("GiftCard", id);
    }

    public async Task<GiftCardBalanceDto> CheckBalanceAsync(string code, CancellationToken ct = default)
    {
        if (_current.UserId is null) throw new ForbiddenException();
        var hash = GiftCardMath.Hash(code);
        var card = await _db.Set<GiftCard>().AsNoTracking().FirstOrDefaultAsync(g => g.CodeHash == hash, ct)
                   ?? throw PlusAccess.Invalid("code", "Gift card not found.");
        return new GiftCardBalanceDto(GiftCardAdjuster.Mask(card.Code), card.Balance, card.Currency, card.ExpiresAt,
            GiftCardMath.IsUsable(card.Status, card.Balance, card.ExpiresAt, DateTime.UtcNow));
    }

    // ---- customer purchase REQUEST (payments are order-bound, so no online charge; admin activates) ----
    public async Task<GiftCardDto> RequestPurchaseAsync(RequestGiftCardPurchaseInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!GiftCardMath.IsValidPurchaseAmount(input.Amount))
            throw PlusAccess.Invalid("amount", $"Amount must be between {GiftCardMath.MinPurchaseAmount:0} and {GiftCardMath.MaxPurchaseAmount:0}.");
        var pending = await _db.Set<GiftCard>().CountAsync(g => g.PurchaserUserId == uid && g.Status == GiftCardStatus.Pending, ct);
        if (pending >= 5) throw new ConflictException("You already have several pending gift card requests.");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = GiftCardMath.GenerateCode();
            var card = new GiftCard
            {
                Code = code, CodeHash = GiftCardMath.Hash(code),
                InitialAmount = Math.Round(input.Amount, 2), Balance = 0m, Currency = "BDT",
                PurchaserUserId = uid, RecipientEmail = input.RecipientEmail?.Trim(), Message = input.Message?.Trim(),
                Status = GiftCardStatus.Pending, CreatedBy = uid
            };
            _db.Set<GiftCard>().Add(card);
            try { await _db.SaveChangesAsync(ct); return ToDto(card, true); }
            catch (DbUpdateException)
            {
                foreach (var en in _db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList()) en.State = EntityState.Detached;
            }
        }
        throw new ConflictException("Could not generate a unique gift card code.");
    }

    public async Task<IReadOnlyList<GiftCardDto>> ListMyPurchasesAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.Set<GiftCard>().AsNoTracking().Where(g => g.PurchaserUserId == uid)
            .OrderByDescending(g => g.CreatedAt).Take(50).ToListAsync(ct);
        return rows.Select(g => ToDto(g, true)).ToList();
    }

    public async Task<GiftCardDto> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();
        var card = await _db.Set<GiftCard>().FirstOrDefaultAsync(g => g.Id == id, ct) ?? throw new NotFoundException("GiftCard", id);
        if (card.Status == GiftCardStatus.Pending)
        {
            // Conditional update keeps activation idempotent under concurrent admin clicks.
            var amount = card.InitialAmount;
            var rows = await _db.Set<GiftCard>().Where(g => g.Id == id && g.Status == GiftCardStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GiftCardStatus.Active).SetProperty(g => g.Balance, amount), ct);
            if (rows > 0)
            {
                _db.Set<GiftCardTransaction>().Add(new GiftCardTransaction
                { GiftCardId = id, Type = GiftCardTransactionType.Issue, Amount = amount, Note = "Purchase confirmed by admin" });
                await _db.SaveChangesAsync(ct);
            }
            await _db.Entry(card).ReloadAsync(ct);
        }
        else if (card.Status != GiftCardStatus.Active) throw new ConflictException("Only pending gift cards can be activated.");
        return ToDto(card);
    }

    /// <summary>Idempotent (unique order/card/type): puts redeemed balance back when an order is cancelled/refunded in full.</summary>
    internal async Task RefundForOrderAsync(Guid orderId, CancellationToken ct)
    {
        var redeems = await _db.Set<GiftCardTransaction>().AsNoTracking()
            .Where(t => t.OrderId == orderId && t.Type == GiftCardTransactionType.Redeem).ToListAsync(ct);
        foreach (var r in redeems)
        {
            var amount = -r.Amount;
            if (amount <= 0m) continue;
            var tx = new GiftCardTransaction
            { GiftCardId = r.GiftCardId, Type = GiftCardTransactionType.Refund, Amount = amount, OrderId = orderId, Note = "Order cancelled/refunded" };
            _db.Set<GiftCardTransaction>().Add(tx);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { _db.Entry(tx).State = EntityState.Detached; continue; } // already refunded

            var gid = r.GiftCardId;
            await _db.Set<GiftCard>().Where(g => g.Id == gid)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Balance, g => g.InitialAmount < g.Balance + amount ? g.InitialAmount : g.Balance + amount), ct);
            await _db.Set<GiftCard>().Where(g => g.Id == gid && g.Status == GiftCardStatus.Depleted && g.Balance > 0m)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GiftCardStatus.Active), ct);
        }
    }

    private static GiftCardDto ToDto(GiftCard g, bool maskUntilActive = false) => new(g.Id, maskUntilActive ? GiftCardMath.DisplayCode(g.Code, g.Status) : g.Code, g.InitialAmount, g.Balance, g.Currency,
        g.RecipientEmail, g.Message, g.ExpiresAt, g.Status, g.CreatedAt);
}

public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public PromotionService(ApplicationDbContext db, ICurrentUserService current) { _db = db; _current = current; }

    private void EnsureAdmin() { if (!PlusAccess.IsAdmin(_current)) throw new ForbiddenException(); }

    public async Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct = default)
    {
        EnsureAdmin();
        var rows = await _db.Set<Promotion>().AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PromotionDto>> ListActivePublicAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Set<Promotion>().AsNoTracking()
            .Where(p => p.IsActive && (p.StartsAt == null || p.StartsAt <= now) && (p.EndsAt == null || p.EndsAt >= now)
                        && (p.UsageLimit == null || p.UsedCount < p.UsageLimit))
            .OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PromotionDto> CreateAsync(UpsertPromotionInput input, CancellationToken ct = default)
    {
        EnsureAdmin();
        Validate(input);
        var p = new Promotion { CreatedByUserId = _current.UserId, CreatedBy = _current.UserId };
        Apply(p, input);
        _db.Set<Promotion>().Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<PromotionDto> UpdateAsync(Guid id, UpsertPromotionInput input, CancellationToken ct = default)
    {
        EnsureAdmin();
        Validate(input);
        var p = await _db.Set<Promotion>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
        Apply(p, input);
        p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = _current.UserId;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();
        var p = await _db.Set<Promotion>().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Promotion", id);
        _db.Set<Promotion>().Remove(p); // soft delete via the context
        await _db.SaveChangesAsync(ct);
    }

    private static void Validate(UpsertPromotionInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Name) || i.Name.Trim().Length > 160) throw PlusAccess.Invalid("name", "Name is required (max 160).");
        if (i.Type == PromotionType.PercentOff && (i.Value <= 0 || i.Value > 100)) throw PlusAccess.Invalid("value", "Percent must be in (0,100].");
        if (i.Type == PromotionType.FixedOff && i.Value <= 0) throw PlusAccess.Invalid("value", "Amount must be positive.");
        if (i.Type == PromotionType.BuyXGetY && (i.BuyQuantity <= 0 || i.GetQuantity <= 0)) throw PlusAccess.Invalid("buyQuantity", "Buy and get quantities must be positive.");
        if (i.Scope != PromotionScope.Storewide && i.ScopeRefId is null) throw PlusAccess.Invalid("scopeRefId", "A target id is required for this scope.");
        if (i.StartsAt is { } s && i.EndsAt is { } e && e <= s) throw PlusAccess.Invalid("endsAt", "End must be after start.");
        if (i.MinSubtotal < 0) throw PlusAccess.Invalid("minSubtotal", "Minimum subtotal cannot be negative.");
        if (i.UsageLimit is <= 0) throw PlusAccess.Invalid("usageLimit", "Usage limit must be positive.");
    }

    private static void Apply(Promotion p, UpsertPromotionInput i)
    {
        p.Name = i.Name.Trim(); p.Type = i.Type; p.Scope = i.Scope;
        p.ScopeRefId = i.Scope == PromotionScope.Storewide ? null : i.ScopeRefId;
        p.Value = i.Value; p.BuyQuantity = i.BuyQuantity; p.GetQuantity = i.GetQuantity;
        p.MinSubtotal = i.MinSubtotal; p.StartsAt = i.StartsAt; p.EndsAt = i.EndsAt;
        p.UsageLimit = i.UsageLimit; p.Stackable = i.Stackable; p.IsActive = i.IsActive;
    }

    private static PromotionDto ToDto(Promotion p) => new(p.Id, p.Name, p.Type, p.Scope, p.ScopeRefId, p.Value,
        p.BuyQuantity, p.GetQuantity, p.MinSubtotal, p.StartsAt, p.EndsAt, p.UsageLimit, p.UsedCount, p.Stackable, p.IsActive, p.CreatedAt);
}

/// <summary>Read-only pricing preview for CartPage/CheckoutPage (runs the pipeline with Commit = false).</summary>
public class CartPricingPreviewService : ICartPricingPreviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IOrderPricingPipeline _pipeline;
    private readonly MarketplaceOptions _market;

    public CartPricingPreviewService(ApplicationDbContext db, ICurrentUserService current,
        IOrderPricingPipeline pipeline, IOptions<MarketplaceOptions> market)
    {
        _db = db; _current = current; _pipeline = pipeline; _market = market.Value;
    }

    public async Task<CartPricingPreviewDto> PreviewAsync(CartPricingPreviewRequest request, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.CartItems.AsNoTracking().Where(c => c.UserId == uid)
            .Select(c => new { c.Id, c.ProductId, c.Product.StoreId, c.Product.CategoryId, c.Quantity, Unit = c.Product.DiscountPrice ?? c.Product.Price })
            .ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToList();
        var links = await _db.Set<CartItemVariant>().AsNoTracking().Where(l => ids.Contains(l.CartItemId))
            .ToDictionaryAsync(l => l.CartItemId, l => l.VariantId, ct);
        var vids = links.Values.Distinct().ToList();
        var prices = await _db.Set<ProductVariant>().AsNoTracking().Where(v => vids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.PriceOverride, ct);

        var lines = rows.Select(r =>
        {
            Guid? vid = links.TryGetValue(r.Id, out var v) ? v : null;
            var unit = vid is { } k && prices.GetValueOrDefault(k) is { } po ? po : r.Unit;
            return new PricingLine(r.ProductId, r.StoreId, r.CategoryId, vid, r.Quantity, unit);
        }).ToList();
        var subtotal = lines.Sum(l => l.Total);

        decimal coupon = 0m; var couponApplied = false;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var c = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(x => x.Code == request.CouponCode!.Trim().ToUpper(), ct);
            if (c is { IsActive: true } && (c.ExpiresAt is null || c.ExpiresAt > DateTime.UtcNow) && subtotal >= c.MinOrderAmount
                && (c.MaxRedemptions is null || c.RedemptionsCount < c.MaxRedemptions))
            {
                coupon = c.Type == CouponType.Percent
                    ? Math.Round(subtotal * c.Value / 100m, 2, MidpointRounding.AwayFromZero) : Math.Min(c.Value, subtotal);
                couponApplied = true;
            }
        }

        var market = _market;
        var result = await _pipeline.ApplyAsync(new OrderPricingRequest(uid, Guid.Empty, lines, subtotal, coupon, couponApplied,
            s => FeeCalculator.Compute(s, market), request.RedeemLoyaltyPoints, request.GiftCardCode, 0m, false), ct);

        return new CartPricingPreviewDto(subtotal, coupon, result.MerchandiseDiscount, result.ShippingFee, result.Tax,
            result.CreditsApplied, result.Total,
            result.Adjustments.Select(a => new AppliedAdjustmentDto(a.Kind, a.Description, a.Amount, a.Points)).ToList());
    }
}
