using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Infrastructure.Features.Reliability;

namespace Pawzaroo.Infrastructure.Features;

public static class ReliabilityRegistration
{
    /// <summary>
    /// Stateless, thread-safe interceptor that rejects Update/Delete of PaymentAuditEntry.
    /// Add to the DbContext options:  opt.AddInterceptors(ReliabilityRegistration.AuditImmutabilityInterceptor)
    /// </summary>
    public static ISaveChangesInterceptor AuditImmutabilityInterceptor { get; } = new PaymentAuditImmutabilityInterceptor();

    /// <summary>
    /// Idempotency store/cleanup, payment audit ledger, retry-safe callback processor, gateway reconciliation.
    /// Call AFTER AddInfrastructure (needs IPaymentGateway, IOrderService, ApplicationDbContext).
    /// </summary>
    public static IServiceCollection AddReliabilityFeatures(this IServiceCollection s)
    {
        s.AddSingleton<IIdempotencyStore, IdempotencyStore>();
        s.AddSingleton<IIdempotencyCleanupService, IdempotencyCleanupService>();

        s.AddScoped<IPaymentAuditService, PaymentAuditService>();
        s.AddScoped<IPaymentCallbackProcessor, PaymentCallbackProcessor>();

        // Optional gateway capability: resolves to the gateway when it implements IPaymentStatusQuery,
        // otherwise to a "not supported" stub (reconciliation then does local-consistency checks only).
        s.AddScoped<IPaymentStatusQuery>(sp =>
            sp.GetRequiredService<IPaymentGateway>() as IPaymentStatusQuery ?? new NullPaymentStatusQuery());
        s.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
        return s;
    }

    private sealed class NullPaymentStatusQuery : IPaymentStatusQuery { }
}
