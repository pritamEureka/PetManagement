using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Features.Reliability;

public class PaymentAuditImmutableException : InvalidOperationException
{
    public PaymentAuditImmutableException()
        : base("PaymentAuditEntry is append-only: updates and deletes are not allowed.") { }
}

/// <summary>
/// Rejects any SaveChanges that would Update or Delete a <see cref="PaymentAuditEntry"/>.
/// Note: ExecuteUpdate/ExecuteDelete bypass SaveChanges; nothing in the code base should use them on this table.
/// </summary>
public sealed class PaymentAuditImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Guard(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public static void Guard(DbContext? ctx)
    {
        if (ctx is null) return;
        foreach (var e in ctx.ChangeTracker.Entries<PaymentAuditEntry>())
            if (e.State is EntityState.Modified or EntityState.Deleted)
                throw new PaymentAuditImmutableException();
    }
}
