using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Payments;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features.Reliability;

/// <summary>
/// Retry-safe gateway callback handling.
///   1. The gateway validates the callback out-of-band (validator API) - redirect params alone are never trusted.
///   2. Insert-first into PaymentCallbackLog, unique on (Provider, TransactionId, EventType), inside one transaction.
///      A second identical callback loses the unique-index race and replays the stored result with no side effects.
///   3. Amount + currency are compared with the order before it may be marked paid.
///   4. Order/payment transition, ledger writes, audit entries and the log row commit atomically.
/// EventType is "{kind}:{outcome}" so a transient validator failure never blocks a later successful retry.
/// </summary>
public class PaymentCallbackProcessor : IPaymentCallbackProcessor
{
    public const string Provider = "sslcommerz";
    private const decimal AmountTolerance = 0.01m;

    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly IOrderService _orders;
    private readonly IPaymentAuditService _audit;
    private readonly SslCommerzOptions _options;

    public PaymentCallbackProcessor(ApplicationDbContext db, IPaymentGateway gateway, IOrderService orders,
        IPaymentAuditService audit, IOptions<SslCommerzOptions> options)
    {
        _db = db;
        _gateway = gateway;
        _orders = orders;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<PaymentCallbackProcessResult?> ProcessAsync(IDictionary<string, string> fields,
        PaymentCallbackKind kind, string? ipAddress, CancellationToken ct = default)
    {
        var result = await _gateway.VerifyCallbackAsync(fields, kind, ct);
        if (result is null)
        {
            await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.CallbackReceived, Provider,
                RawPayload: fields, IpAddress: ipAddress, Note: $"{kind}: unvalidated (no order id)"), ct);
            return null;
        }

        var txnId = Trunc(string.IsNullOrWhiteSpace(result.ProviderRef) ? result.OrderId.ToString() : result.ProviderRef, 128);
        var eventType = $"{kind}:{result.Outcome}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var log = new PaymentCallbackLog
        {
            Provider = Provider, TransactionId = txnId, EventType = eventType,
            OrderId = result.OrderId, Outcome = result.Outcome.ToString(), Result = "Pending"
        };
        _db.Set<PaymentCallbackLog>().Add(log);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.Entry(log).State = EntityState.Detached;
            await tx.RollbackAsync(ct);

            var existing = await _db.Set<PaymentCallbackLog>().AsNoTracking()
                .FirstOrDefaultAsync(l => l.Provider == Provider && l.TransactionId == txnId && l.EventType == eventType, ct);
            if (existing is null) throw; // not a duplicate - a genuine DB failure

            await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.DuplicateCallback, Provider,
                OrderId: result.OrderId, RawPayload: fields, IpAddress: ipAddress,
                Note: $"{eventType} {txnId} already handled ({existing.Result})"), ct);
            return new PaymentCallbackProcessResult(result.OrderId, result.Outcome, true, existing.Result);
        }

        var paymentId = await _db.Payments.AsNoTracking().Where(p => p.OrderId == result.OrderId)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        var currency = result.Currency;

        await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.CallbackReceived, Provider,
            result.OrderId, paymentId, result.AmountValidated, currency, fields, ipAddress, $"{kind}"), ct);
        await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.Validated, Provider,
            result.OrderId, paymentId, result.AmountValidated, currency, null, ipAddress,
            $"{kind} -> {result.Outcome} ref={txnId}"), ct);

        string applied;
        switch (result.Outcome)
        {
            case PaymentCallbackOutcome.Succeeded:
            {
                var order = await _db.Orders.AsNoTracking().Where(o => o.Id == result.OrderId)
                    .Select(o => new { o.Total }).FirstOrDefaultAsync(ct);
                if (order is null) { applied = "Ignored"; break; }

                var expectedCurrency = string.IsNullOrWhiteSpace(_options.Currency) ? "BDT" : _options.Currency;
                var amountOk = result.AmountValidated.HasValue
                               && Math.Abs(result.AmountValidated.Value - order.Total) <= AmountTolerance;
                var currencyOk = !string.IsNullOrWhiteSpace(currency)
                                 && string.Equals(currency, expectedCurrency, StringComparison.OrdinalIgnoreCase);
                if (!amountOk || !currencyOk)
                {
                    applied = "AmountMismatch";
                    await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.AmountMismatch, Provider,
                        result.OrderId, paymentId, result.AmountValidated, currency, null, ipAddress,
                        $"expected {order.Total:0.00} {expectedCurrency}, gateway {result.AmountValidated?.ToString("0.00") ?? "n/a"} {currency ?? "n/a"}"), ct);
                    break;
                }

                await _orders.MarkPaymentSucceededAsync(result.OrderId, result.ProviderRef, result.AmountValidated, ct);
                applied = "Applied";
                await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.Succeeded, Provider,
                    result.OrderId, paymentId, result.AmountValidated, currency, null, ipAddress, txnId), ct);
                break;
            }
            case PaymentCallbackOutcome.Failed:
            case PaymentCallbackOutcome.Cancelled:
            {
                var cancelled = result.Outcome == PaymentCallbackOutcome.Cancelled;
                await _orders.MarkPaymentFailedAsync(result.OrderId, result.ProviderRef, cancelled, ct);
                applied = "Applied";
                await _audit.RecordAsync(new PaymentAuditRecord(
                    cancelled ? PaymentAuditEventType.Cancelled : PaymentAuditEventType.Failed, Provider,
                    result.OrderId, paymentId, null, null, null, ipAddress, txnId), ct);
                break;
            }
            default:
                applied = "Ignored";
                break;
        }

        log.Result = applied;
        log.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PaymentCallbackProcessResult(result.OrderId, result.Outcome, false, applied);
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];
}
