using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Admin payment reconciliation: local consistency + gateway status comparison. Never mutates orders.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/payments/reconcile")]
public class PaymentReconciliationController : ControllerBase
{
    private const int MaxRangeDays = 90;
    private readonly IPaymentReconciliationService _svc;
    private readonly IPaymentAuditService _audit;

    public PaymentReconciliationController(IPaymentReconciliationService svc, IPaymentAuditService audit)
    {
        _svc = svc;
        _audit = audit;
    }

    public record ReconcileRequest(DateTime? From, DateTime? To);

    /// <summary>Runs the reconciliation (read-only). Defaults to the last 7 days.</summary>
    [HttpGet]
    [Permission(Permissions.Settings.View)]
    public async Task<ReconciliationReport> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (f, t) = Range(from, to);
        return await _svc.ReconcileAsync(f, t, ct);
    }

    /// <summary>Runs the reconciliation and appends one immutable "Reconciled" audit entry per discrepancy.</summary>
    [HttpPost]
    [Permission(Permissions.Settings.Edit)]
    public async Task<ReconciliationReport> Run([FromBody] ReconcileRequest? body, CancellationToken ct)
    {
        var (f, t) = Range(body?.From, body?.To);
        var report = await _svc.ReconcileAsync(f, t, ct);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        foreach (var item in report.Items.Where(i => i.Severity == "critical"))
            await _audit.RecordAsync(new PaymentAuditRecord(PaymentAuditEventType.Reconciled, "sslcommerz",
                OrderId: item.OrderId, PaymentId: item.PaymentId, Amount: item.GatewayAmount ?? item.LocalAmount,
                IpAddress: ip, Note: $"{item.Kind}: {item.Detail}"), ct);
        return report;
    }

    private static (DateTime From, DateTime To) Range(DateTime? from, DateTime? to)
    {
        var t = (to ?? DateTime.UtcNow).ToUniversalTime();
        var f = (from ?? t.AddDays(-7)).ToUniversalTime();
        if (f > t) (f, t) = (t, f);
        if ((t - f).TotalDays > MaxRangeDays) f = t.AddDays(-MaxRangeDays);
        return (f, t);
    }
}
