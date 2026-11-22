using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features.Reliability;

/// <summary>
/// Read-only reconciliation. Two layers:
///   - Local consistency (always): Paid online orders with no Succeeded audit entry; online payments stuck Pending.
///   - Gateway comparison (when IPaymentStatusQuery.SupportsQuery): status + amount of each online payment vs gateway.
/// Never mutates orders/payments; discrepancies are for an operator to resolve.
/// </summary>
public class PaymentReconciliationService : IPaymentReconciliationService
{
    public static readonly TimeSpan PendingThreshold = TimeSpan.FromMinutes(30);
    private const int MaxGatewayQueries = 200;
    private const decimal Tolerance = 0.01m;
    private const string Online = "sslcommerz";

    private readonly ApplicationDbContext _db;
    private readonly IPaymentStatusQuery _gateway;

    public PaymentReconciliationService(ApplicationDbContext db, IPaymentStatusQuery gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (to < from) (from, to) = (to, from);
        var now = DateTime.UtcNow;
        var report = new ReconciliationReport { From = from, To = to, GeneratedAt = now, GatewayQuerySupported = _gateway.SupportsQuery };

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to && p.Method == Online)
            .Select(p => new { p.Id, p.OrderId, p.Amount, p.Status, p.CreatedAt, OrderStatus = p.Order.PaymentStatus, OrderTotal = p.Order.Total })
            .OrderBy(p => p.CreatedAt)
            .Take(5000)
            .ToListAsync(ct);
        report.PaymentsChecked = payments.Count;

        var orderIds = payments.Select(p => p.OrderId).Distinct().ToList();
        var succeeded = (await _db.Set<PaymentAuditEntry>().AsNoTracking()
                .Where(e => e.EventType == PaymentAuditEventType.Succeeded && e.OrderId != null && orderIds.Contains(e.OrderId.Value))
                .Select(e => e.OrderId!.Value).Distinct().ToListAsync(ct))
            .ToHashSet();

        var queried = 0;
        foreach (var p in payments)
        {
            var before = report.Items.Count;
            var localPaid = p.OrderStatus == PaymentStatus.Paid || p.Status == PaymentStatus.Paid;

            if (p.OrderStatus == PaymentStatus.Paid && !succeeded.Contains(p.OrderId))
                report.Items.Add(new ReconciliationItem(p.OrderId, p.Id, "PaidWithoutAuditEntry", "warning",
                    "Order is Paid but no Succeeded payment audit entry exists (paid before the audit ledger, or marked outside the callback flow).",
                    p.Amount, null, p.OrderStatus.ToString(), null));

            if (p.Status == PaymentStatus.Pending && now - p.CreatedAt > PendingThreshold)
                report.Items.Add(new ReconciliationItem(p.OrderId, p.Id, "StalePending", "info",
                    $"Payment pending for more than {(int)PendingThreshold.TotalMinutes} minutes.",
                    p.Amount, null, p.Status.ToString(), null));

            if (_gateway.SupportsQuery && queried < MaxGatewayQueries
                && (localPaid || p.Status is PaymentStatus.Pending or PaymentStatus.Failed))
            {
                queried++;
                var g = await _gateway.QueryByTransactionIdAsync(p.OrderId.ToString(), ct);
                if (g is not null)
                {
                    if (g.Paid && !localPaid)
                        report.Items.Add(new ReconciliationItem(p.OrderId, p.Id, "GatewayPaidLocalNot", "critical",
                            "Gateway reports the transaction as paid but the order is not Paid locally (missed callback).",
                            p.Amount, g.Amount, p.Status.ToString(), g.RawStatus));
                    else if (!g.Paid && localPaid)
                        report.Items.Add(new ReconciliationItem(p.OrderId, p.Id, "LocalPaidGatewayNot", "critical",
                            g.Found ? "Order is Paid locally but the gateway does not show a valid payment."
                                    : "Order is Paid locally but the gateway has no record of the transaction.",
                            p.Amount, g.Amount, p.Status.ToString(), g.RawStatus ?? (g.Found ? null : "NOT_FOUND")));
                    else if (g.Paid && g.Amount.HasValue && Math.Abs(g.Amount.Value - p.OrderTotal) > Tolerance)
                        report.Items.Add(new ReconciliationItem(p.OrderId, p.Id, "AmountMismatch", "critical",
                            "Gateway amount differs from the order total.",
                            p.OrderTotal, g.Amount, p.Status.ToString(), g.RawStatus));
                }
            }

            if (report.Items.Count == before) report.Matched++;
        }
        return report;
    }
}
