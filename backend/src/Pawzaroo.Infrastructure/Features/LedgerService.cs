using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Vendor ledger + settlement batches. All money is decimal, rounded to 2dp away-from-zero.
/// Ledger rows are append-only; idempotency comes from the unique (OrderItemId, Type) index
/// plus an existence pre-check.
/// </summary>
public class LedgerService : ILedgerRecorder, ISettlementService
{
    public const string HoldDaysSettingKey = "settlement.hold_days";

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IConfiguration _config;
    private readonly IAuditLogger _audit;
    private readonly INotificationService _notify;
    private readonly ILogger<LedgerService> _log;

    public LedgerService(ApplicationDbContext db, ICurrentUserService current, IConfiguration config,
        IAuditLogger audit, INotificationService notify, ILogger<LedgerService> log)
    {
        _db = db;
        _current = current;
        _config = config;
        _audit = audit;
        _notify = notify;
        _log = log;
    }

    private string Currency => string.IsNullOrWhiteSpace(_config["Settlement:Currency"]) ? "BDT" : _config["Settlement:Currency"]!;

    // ------------------------------------------------------------------ hold config

    public async Task<int> GetHoldDaysAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await _db.Set<SystemSetting>().AsNoTracking()
                .Where(s => s.Key == HoldDaysSettingKey).Select(s => s.ValueJson).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var el = doc.RootElement;
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("value", out var v)) el = v;
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return Math.Clamp(n, 0, 365);
                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var ns)) return Math.Clamp(ns, 0, 365);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _log.LogWarning(ex, "Invalid {Key} setting; falling back to config.", HoldDaysSettingKey);
        }
        return int.TryParse(_config["Settlement:HoldDays"], out var cfg) ? Math.Clamp(cfg, 0, 365) : 7;
    }

    // ------------------------------------------------------------------ recorder (order hooks)

    public async Task RecordOrderEarningsAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId).ToListAsync(ct);
            if (items.Count == 0) return;
            var itemIds = items.Select(i => (Guid?)i.Id).ToList();

            var existing = await _db.Set<LedgerEntry>().AsNoTracking()
                .Where(e => itemIds.Contains(e.OrderItemId) && (e.Type == LedgerEntryType.Earning || e.Type == LedgerEntryType.Commission))
                .Select(e => new { e.OrderItemId, e.Type }).ToListAsync(ct);
            var have = existing.Select(e => (e.OrderItemId!.Value, e.Type)).ToHashSet();

            var now = DateTime.UtcNow;
            var availableAt = now.AddDays(await GetHoldDaysAsync(ct));
            var added = false;
            // Order-level coupon discount is allocated pro-rata across item lines (see LedgerMath.AllocateProRata):
            // earning_i = item.Total - discount * item.Total / sum(item.Total). Commission stays on the gross line.
            var discount = await _db.Orders.AsNoTracking().Where(o => o.Id == orderId)
                .Select(o => o.DiscountAmount).FirstOrDefaultAsync(ct);
            var net = LedgerMath.NetOfDiscount(items.Select(x => x.Total).ToList(), discount);
            for (var idx = 0; idx < items.Count; idx++)
            {
                var i = items[idx];
                if (!have.Contains((i.Id, LedgerEntryType.Earning)) && net[idx] != 0m)
                {
                    _db.Set<LedgerEntry>().Add(new LedgerEntry
                    {
                        StoreId = i.StoreId, OrderId = orderId, OrderItemId = i.Id, Type = LedgerEntryType.Earning,
                        Amount = Round(net[idx]), Currency = Currency, AvailableAt = availableAt, CreatedAt = now,
                        Memo = "Item sale"
                    });
                    added = true;
                }
                if (!have.Contains((i.Id, LedgerEntryType.Commission)) && i.CommissionAmount != 0m)
                {
                    _db.Set<LedgerEntry>().Add(new LedgerEntry
                    {
                        StoreId = i.StoreId, OrderId = orderId, OrderItemId = i.Id, Type = LedgerEntryType.Commission,
                        Amount = -Round(i.CommissionAmount), Currency = Currency, AvailableAt = availableAt, CreatedAt = now,
                        Memo = "Platform commission"
                    });
                    added = true;
                }
            }
            if (added) await _db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            DetachPendingLedgerEntries();
            _log.LogError(ex, "Failed to record ledger earnings for order {OrderId}", orderId);
        }
    }

    public async Task RecordOrderRefundAsync(Guid orderId, decimal? amount, CancellationToken ct = default)
    {
        try
        {
            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is null || order.Total <= 0m) return;
            var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId).ToListAsync(ct);
            if (items.Count == 0) return;
            var itemIds = items.Select(i => (Guid?)i.Id).ToList();

            var existing = await _db.Set<LedgerEntry>().AsNoTracking()
                .Where(e => itemIds.Contains(e.OrderItemId))
                .Select(e => new { e.OrderItemId, e.Type }).ToListAsync(ct);
            var have = existing.Select(e => (e.OrderItemId!.Value, e.Type)).ToHashSet();

            // null amount = full refund of the item lines; otherwise pro-rata on the order total.
            var ratio = LedgerMath.RefundRatio(amount, order.Total);
            if (ratio <= 0m) return;

            // Refund the discount-net amount the store was actually credited (same allocation as earnings).
            var nets = LedgerMath.NetOfDiscount(items.Select(x => x.Total).ToList(), order.DiscountAmount);
            var netByItem = new Dictionary<Guid, decimal>();
            for (var n = 0; n < items.Count; n++) netByItem[items[n].Id] = nets[n];

            var now = DateTime.UtcNow;
            var added = false;
            foreach (var i in items)
            {
                // Only reverse money that was actually credited to the store.
                if (!have.Contains((i.Id, LedgerEntryType.Earning))) continue;

                if (!have.Contains((i.Id, LedgerEntryType.Refund)))
                {
                    var refund = Round(netByItem[i.Id] * ratio);
                    if (refund != 0m)
                    {
                        _db.Set<LedgerEntry>().Add(new LedgerEntry
                        {
                            StoreId = i.StoreId, OrderId = orderId, OrderItemId = i.Id, Type = LedgerEntryType.Refund,
                            Amount = -refund, Currency = Currency, AvailableAt = now, CreatedAt = now, Memo = "Customer refund"
                        });
                        added = true;
                    }
                }
                if (have.Contains((i.Id, LedgerEntryType.Commission)) && !have.Contains((i.Id, LedgerEntryType.Reversal)))
                {
                    var rev = Round(i.CommissionAmount * ratio);
                    if (rev != 0m)
                    {
                        _db.Set<LedgerEntry>().Add(new LedgerEntry
                        {
                            StoreId = i.StoreId, OrderId = orderId, OrderItemId = i.Id, Type = LedgerEntryType.Reversal,
                            Amount = rev, Currency = Currency, AvailableAt = now, CreatedAt = now, Memo = "Commission reversal on refund"
                        });
                        added = true;
                    }
                }
            }
            if (added) await _db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            DetachPendingLedgerEntries();
            _log.LogError(ex, "Failed to record ledger refund for order {OrderId}", orderId);
        }
    }

    public async Task RecordItemRefundAsync(Guid orderItemId, decimal? amount, CancellationToken ct = default)
    {
        try
        {
            var item = await _db.OrderItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == orderItemId, ct);
            if (item is null) return;
            var siblings = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == item.OrderId).ToListAsync(ct);
            var discount = await _db.Orders.AsNoTracking().Where(o => o.Id == item.OrderId)
                .Select(o => o.DiscountAmount).FirstOrDefaultAsync(ct);
            var nets = LedgerMath.NetOfDiscount(siblings.Select(x => x.Total).ToList(), discount);
            var net = nets[siblings.FindIndex(x => x.Id == orderItemId)];
            if (net <= 0m) return;

            var have = (await _db.Set<LedgerEntry>().AsNoTracking().Where(e => e.OrderItemId == orderItemId)
                .Select(e => e.Type).ToListAsync(ct)).ToHashSet();
            // Only reverse money actually credited; Refund/Reversal are once per item (idempotent).
            if (!have.Contains(LedgerEntryType.Earning) || have.Contains(LedgerEntryType.Refund)) return;

            var refund = Round(amount.HasValue ? Math.Min(Math.Max(amount.Value, 0m), net) : net);
            if (refund <= 0m) return;

            var now = DateTime.UtcNow;
            _db.Set<LedgerEntry>().Add(new LedgerEntry
            {
                StoreId = item.StoreId, OrderId = item.OrderId, OrderItemId = orderItemId, Type = LedgerEntryType.Refund,
                Amount = -refund, Currency = Currency, AvailableAt = now, CreatedAt = now, Memo = "Return refund"
            });
            if (have.Contains(LedgerEntryType.Commission) && !have.Contains(LedgerEntryType.Reversal))
            {
                var rev = LedgerMath.CommissionReversal(item.CommissionAmount, refund, net);
                if (rev != 0m)
                    _db.Set<LedgerEntry>().Add(new LedgerEntry
                    {
                        StoreId = item.StoreId, OrderId = item.OrderId, OrderItemId = orderItemId, Type = LedgerEntryType.Reversal,
                        Amount = rev, Currency = Currency, AvailableAt = now, CreatedAt = now, Memo = "Commission reversal on return"
                    });
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            DetachPendingLedgerEntries();
            _log.LogError(ex, "Failed to record ledger refund for order item {ItemId}", orderItemId);
        }
    }

    private void DetachPendingLedgerEntries()
    {
        foreach (var e in _db.ChangeTracker.Entries<LedgerEntry>().Where(e => e.State == EntityState.Added).ToList())
            e.State = EntityState.Detached;
    }

    // ------------------------------------------------------------------ store side

    private async Task<Guid> MyStoreIdAsync(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var id = await _db.Stores.AsNoTracking().Where(s => s.OwnerUserId == uid).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
        return id ?? throw new NotFoundException("Store", uid);
    }

    public async Task<StoreLedgerSummaryDto> GetMyLedgerAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var storeId = await MyStoreIdAsync(ct);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var now = DateTime.UtcNow;

        var all = _db.Set<LedgerEntry>().AsNoTracking().Where(e => e.StoreId == storeId);
        var unsettled = all.Where(e => e.Type != LedgerEntryType.Payout
            && !_db.Set<StorePayoutLine>().Any(l => l.LedgerEntryId == e.Id));

        var total = await all.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var available = await unsettled.Where(e => e.AvailableAt <= now).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var hold = await unsettled.Where(e => e.AvailableAt > now).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;
        var paid = -(await all.Where(e => e.Type == LedgerEntryType.Payout).SumAsync(e => (decimal?)e.Amount, ct) ?? 0m);

        var count = await all.CountAsync(ct);
        var rows = await all.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new StoreLedgerSummaryDto(storeId, Currency, await GetHoldDaysAsync(ct),
            total, available, hold, paid,
            new PageResult<LedgerEntryDto>(rows.Select(r => ToDto(r)).ToList(), count, page, pageSize));
    }

    public async Task<IReadOnlyList<StorePayoutDto>> ListMyPayoutsAsync(CancellationToken ct = default)
    {
        var storeId = await MyStoreIdAsync(ct);
        var rows = await _db.Set<StorePayout>().AsNoTracking().Include(p => p.SettlementBatch)
            .Where(p => p.StoreId == storeId).OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct);
        var name = await _db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "";
        return rows.Select(p => ToDto(p, name)).ToList();
    }

    public async Task<CsvFile> ExportMyStatementAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var storeId = await MyStoreIdAsync(ct);
        return await BuildStatementAsync(storeId, from, to, ct);
    }

    // ------------------------------------------------------------------ admin side

    private void EnsureSettings(bool write)
    {
        var perm = write ? Permissions.Settings.Edit : Permissions.Settings.View;
        if (!_current.Permissions.Contains(perm)) throw new ForbiddenException();
    }

    public async Task<PageResult<SettlementBatchDto>> ListBatchesAsync(int page, int pageSize, CancellationToken ct = default)
    {
        EnsureSettings(false);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Set<SettlementBatch>().AsNoTracking();
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<SettlementBatchDto>(rows.Select(r => ToDto(r)).ToList(), total, page, pageSize);
    }

    public async Task<SettlementBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        EnsureSettings(false);
        var batch = await _db.Set<SettlementBatch>().AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, ct)
                    ?? throw new NotFoundException("SettlementBatch", batchId);
        var payouts = await _db.Set<StorePayout>().AsNoTracking().Where(p => p.SettlementBatchId == batchId)
            .OrderByDescending(p => p.Amount).ToListAsync(ct);
        var names = await StoreNamesAsync(payouts.Select(p => p.StoreId), ct);
        return new SettlementBatchDetailDto(ToDto(batch),
            payouts.Select(p => ToDto(p, batch.Reference, names.GetValueOrDefault(p.StoreId, ""))).ToList());
    }

    public async Task<PageResult<StorePayoutDto>> ListPayoutsAsync(StorePayoutStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        EnsureSettings(false);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Set<StorePayout>().AsNoTracking().Include(p => p.SettlementBatch).AsQueryable();
        if (status.HasValue) q = q.Where(p => p.Status == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var names = await StoreNamesAsync(rows.Select(p => p.StoreId), ct);
        return new PageResult<StorePayoutDto>(rows.Select(p => ToDto(p, names.GetValueOrDefault(p.StoreId, ""))).ToList(), total, page, pageSize);
    }

    public async Task<SettlementBatchDetailDto?> GenerateBatchAsync(GenerateBatchInput input, CancellationToken ct = default)
    {
        EnsureSettings(true);
        var now = DateTime.UtcNow;
        var cutoff = input.CutoffAt.HasValue ? DeliveryTrackingService.AsUtc(input.CutoffAt.Value) : now;
        if (cutoff > now) cutoff = now; // never settle money still on hold
        var minimum = Math.Max(input.MinimumPayout ?? 0m, 0.01m);
        var currency = Currency;

        var eligible = await _db.Set<LedgerEntry>().AsNoTracking()
            .Where(e => e.Type != LedgerEntryType.Payout && e.Currency == currency && e.AvailableAt <= cutoff
                && !_db.Set<StorePayoutLine>().Any(l => l.LedgerEntryId == e.Id))
            .Select(e => new { e.Id, e.StoreId, e.Amount })
            .ToListAsync(ct);
        if (eligible.Count == 0) return null;

        var perStore = eligible.GroupBy(e => e.StoreId)
            .Select(g => new { StoreId = g.Key, Amount = g.Sum(x => x.Amount), Ids = g.Select(x => x.Id).ToList() })
            .Where(g => g.Amount >= minimum)
            .ToList();
        if (perStore.Count == 0) return null;

        var batch = new SettlementBatch
        {
            Reference = $"SB-{now:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}",
            Currency = currency, CutoffAt = cutoff, CreatedAt = now, CreatedBy = _current.UserId,
            PayoutCount = perStore.Count, TotalAmount = Round(perStore.Sum(p => p.Amount))
        };
        _db.Set<SettlementBatch>().Add(batch);
        foreach (var s in perStore)
        {
            var payout = new StorePayout
            {
                SettlementBatchId = batch.Id, StoreId = s.StoreId, Amount = Round(s.Amount),
                Currency = currency, CreatedAt = now
            };
            _db.Set<StorePayout>().Add(payout);
            foreach (var id in s.Ids)
                _db.Set<StorePayoutLine>().Add(new StorePayoutLine { StorePayoutId = payout.Id, LedgerEntryId = id });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _log.LogWarning(ex, "Settlement batch generation raced with another batch.");
            throw new ConflictException("Some ledger entries were settled by a concurrent batch. Retry.");
        }

        await _audit.LogAsync("settlement.batch.generate", "SettlementBatch", batch.Id.ToString(), batch.Reference, ct: ct);
        return await GetBatchAsync(batch.Id, ct);
    }

    public async Task MarkPayoutPaidAsync(Guid payoutId, MarkPayoutPaidInput input, CancellationToken ct = default)
    {
        EnsureSettings(true);
        var reference = input.GatewayReference?.Trim();
        if (string.IsNullOrEmpty(reference) || reference.Length > 128)
            throw new ValidationException(new Dictionary<string, string[]> { ["gatewayReference"] = new[] { "Gateway reference is required (max 128 chars)." } });

        var payout = await _db.Set<StorePayout>().Include(p => p.SettlementBatch).FirstOrDefaultAsync(p => p.Id == payoutId, ct)
                     ?? throw new NotFoundException("StorePayout", payoutId);
        if (payout.Status != StorePayoutStatus.Pending)
            throw new ConflictException($"Payout is already {payout.Status}.");
        if (await _db.Set<StorePayout>().AnyAsync(p => p.GatewayReference == reference && p.Id != payoutId, ct))
            throw new ConflictException("That gateway reference is already used by another payout.");

        var now = DateTime.UtcNow;
        payout.Status = StorePayoutStatus.Paid;
        payout.GatewayReference = reference;
        payout.GatewayAmount = input.GatewayAmount.HasValue ? Round(input.GatewayAmount.Value) : null;
        if (!string.IsNullOrWhiteSpace(input.Notes)) payout.Notes = input.Notes.Trim();
        payout.PaidAt = now;

        _db.Set<LedgerEntry>().Add(new LedgerEntry
        {
            StoreId = payout.StoreId, Type = LedgerEntryType.Payout, Amount = -payout.Amount,
            Currency = payout.Currency, AvailableAt = now, CreatedAt = now,
            Memo = $"Payout {payout.SettlementBatch.Reference} ({reference})"
        });
        await _db.SaveChangesAsync(ct);
        await RefreshBatchStatusAsync(payout.SettlementBatch, ct);

        var owner = await _db.Stores.AsNoTracking().Where(s => s.Id == payout.StoreId).Select(s => s.OwnerUserId).FirstOrDefaultAsync(ct);
        if (owner != Guid.Empty)
            await _notify.NotifyUserAsync(owner, "Payout sent",
                $"A payout of {payout.Amount:0.00} {payout.Currency} was sent (ref {reference}).",
                new { payoutId = payout.Id }, ct);
        await _audit.LogAsync("settlement.payout.paid", "StorePayout", payout.Id.ToString(), reference, ct: ct);
    }

    public async Task FailPayoutAsync(Guid payoutId, string? reason, CancellationToken ct = default)
    {
        EnsureSettings(true);
        var payout = await _db.Set<StorePayout>().Include(p => p.SettlementBatch).FirstOrDefaultAsync(p => p.Id == payoutId, ct)
                     ?? throw new NotFoundException("StorePayout", payoutId);
        if (payout.Status != StorePayoutStatus.Pending)
            throw new ConflictException($"Only pending payouts can be failed (this one is {payout.Status}).");

        payout.Status = StorePayoutStatus.Failed;
        payout.Notes = string.IsNullOrWhiteSpace(reason) ? payout.Notes : reason.Trim();
        // Release the entries so the next batch can pick them up again.
        var lines = await _db.Set<StorePayoutLine>().Where(l => l.StorePayoutId == payoutId).ToListAsync(ct);
        _db.Set<StorePayoutLine>().RemoveRange(lines);
        await _db.SaveChangesAsync(ct);
        await RefreshBatchStatusAsync(payout.SettlementBatch, ct);
        await _audit.LogAsync("settlement.payout.failed", "StorePayout", payout.Id.ToString(), reason, ct: ct);
    }

    public async Task<ReconcileResultDto> ReconcileBatchAsync(Guid batchId, ReconcileInput input, CancellationToken ct = default)
    {
        EnsureSettings(true);
        var batch = await _db.Set<SettlementBatch>().FirstOrDefaultAsync(b => b.Id == batchId, ct)
                    ?? throw new NotFoundException("SettlementBatch", batchId);
        var payouts = await _db.Set<StorePayout>().Where(p => p.SettlementBatchId == batchId).ToListAsync(ct);
        var byId = payouts.ToDictionary(p => p.Id);

        int matched = 0;
        var mismatched = new List<Guid>();
        var now = DateTime.UtcNow;
        foreach (var line in input.Lines ?? Array.Empty<ReconcileLine>())
        {
            if (!byId.TryGetValue(line.PayoutId, out var p)) continue;
            if (p.Status is not (StorePayoutStatus.Paid or StorePayoutStatus.Reconciled)) continue;

            p.GatewayAmount = Round(line.GatewayAmount);
            if (!string.IsNullOrWhiteSpace(line.GatewayReference) && line.GatewayReference.Trim().Length <= 128
                && string.IsNullOrEmpty(p.GatewayReference))
                p.GatewayReference = line.GatewayReference.Trim();

            if (p.GatewayAmount == p.Amount)
            {
                p.Status = StorePayoutStatus.Reconciled;
                p.ReconciledAt = now;
                matched++;
            }
            else
            {
                if (p.Status == StorePayoutStatus.Reconciled) { p.Status = StorePayoutStatus.Paid; p.ReconciledAt = null; }
                p.Notes = $"Reconcile mismatch: expected {p.Amount:0.00}, gateway {p.GatewayAmount:0.00}";
                mismatched.Add(p.Id);
            }
        }
        await _db.SaveChangesAsync(ct);
        await RefreshBatchStatusAsync(batch, ct);
        await _audit.LogAsync("settlement.batch.reconcile", "SettlementBatch", batch.Id.ToString(),
            $"matched={matched} mismatched={mismatched.Count}", ct: ct);
        return new ReconcileResultDto(matched, mismatched.Count, mismatched, ToDto(batch));
    }

    public async Task<CsvFile> ExportBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var detail = await GetBatchAsync(batchId, ct);
        var sb = new StringBuilder();
        sb.AppendLine("BatchReference,PayoutId,StoreId,StoreName,Amount,Currency,Status,GatewayReference,GatewayAmount,PaidAt,ReconciledAt");
        foreach (var p in detail.Payouts)
            sb.AppendLine(string.Join(',',
                Csv(detail.Batch.Reference), p.Id, p.StoreId, Csv(p.StoreName), Money(p.Amount), p.Currency, p.Status,
                Csv(p.GatewayReference), p.GatewayAmount.HasValue ? Money(p.GatewayAmount.Value) : "",
                Iso(p.PaidAt), Iso(p.ReconciledAt)));
        return new CsvFile($"settlement-{detail.Batch.Reference}.csv", sb.ToString());
    }

    public async Task<CsvFile> ExportStoreStatementAsync(Guid storeId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        EnsureSettings(false);
        return await BuildStatementAsync(storeId, from, to, ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<CsvFile> BuildStatementAsync(Guid storeId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = _db.Set<LedgerEntry>().AsNoTracking().Where(e => e.StoreId == storeId);
        if (from.HasValue) { var f = DeliveryTrackingService.AsUtc(from.Value); q = q.Where(e => e.CreatedAt >= f); }
        if (to.HasValue) { var t = DeliveryTrackingService.AsUtc(to.Value); q = q.Where(e => e.CreatedAt <= t); }
        var rows = await q.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).Take(50000).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Type,OrderId,OrderItemId,Amount,Currency,AvailableAt,Memo,RunningBalance");
        decimal running = 0m;
        foreach (var e in rows)
        {
            running += e.Amount;
            sb.AppendLine(string.Join(',', Iso(e.CreatedAt), e.Type, e.OrderId?.ToString() ?? "", e.OrderItemId?.ToString() ?? "",
                Money(e.Amount), e.Currency, Iso(e.AvailableAt), Csv(e.Memo), Money(running)));
        }
        sb.AppendLine(string.Join(',', "TOTAL", "", "", "", Money(running), Currency, "", "", ""));
        return new CsvFile($"ledger-statement-{storeId:N}-{DateTime.UtcNow:yyyyMMdd}.csv", sb.ToString());
    }

    private async Task RefreshBatchStatusAsync(SettlementBatch batch, CancellationToken ct)
    {
        var statuses = await _db.Set<StorePayout>().AsNoTracking().Where(p => p.SettlementBatchId == batch.Id)
            .Select(p => p.Status).ToListAsync(ct);
        var live = statuses.Where(s => s != StorePayoutStatus.Failed).ToList();
        var now = DateTime.UtcNow;

        var next = batch.Status;
        if (live.Count > 0 && live.All(s => s == StorePayoutStatus.Reconciled)) next = SettlementBatchStatus.Reconciled;
        else if (live.Count > 0 && live.All(s => s is StorePayoutStatus.Paid or StorePayoutStatus.Reconciled)) next = SettlementBatchStatus.Paid;
        else next = SettlementBatchStatus.Generated;

        if (next != batch.Status)
        {
            batch.Status = next;
            if (next == SettlementBatchStatus.Paid && batch.PaidAt is null) batch.PaidAt = now;
            if (next == SettlementBatchStatus.Reconciled) { batch.PaidAt ??= now; batch.ReconciledAt = now; }
            if (next == SettlementBatchStatus.Generated) { batch.PaidAt = null; batch.ReconciledAt = null; }
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<Dictionary<Guid, string>> StoreNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.Stores.AsNoTracking().Where(s => list.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Iso(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : "";

    /// <summary>RFC4180 quoting plus a guard against spreadsheet formula injection.</summary>
    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r') s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static LedgerEntryDto ToDto(LedgerEntry e) => new(
        e.Id, e.StoreId, e.OrderId, e.OrderItemId, e.Type, e.Amount, e.Currency, e.AvailableAt, e.Memo, e.CreatedAt);

    private static SettlementBatchDto ToDto(SettlementBatch b) => new(
        b.Id, b.Reference, b.Status, b.Currency, b.TotalAmount, b.PayoutCount, b.CutoffAt, b.CreatedAt, b.PaidAt, b.ReconciledAt);

    private static StorePayoutDto ToDto(StorePayout p, string storeName) =>
        ToDto(p, p.SettlementBatch?.Reference ?? "", storeName);

    private static StorePayoutDto ToDto(StorePayout p, string batchReference, string storeName) => new(
        p.Id, p.SettlementBatchId, batchReference, p.StoreId, storeName, p.Amount, p.Currency, p.Status,
        p.GatewayReference, p.GatewayAmount, p.Notes, p.CreatedAt, p.PaidAt, p.ReconciledAt);
}
