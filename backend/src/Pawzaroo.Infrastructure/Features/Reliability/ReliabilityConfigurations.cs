using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Features.Reliability;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records");
        b.HasIndex(r => new { r.UserId, r.Key, r.Method, r.Path }).IsUnique();
        b.HasIndex(r => r.ExpiresAt);
        b.Property(r => r.Key).HasMaxLength(128).IsRequired();
        b.Property(r => r.Method).HasMaxLength(10).IsRequired();
        b.Property(r => r.Path).HasMaxLength(512).IsRequired();
        b.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(r => r.ContentType).HasMaxLength(128);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
    }
}

public class PaymentAuditEntryConfiguration : IEntityTypeConfiguration<PaymentAuditEntry>
{
    public void Configure(EntityTypeBuilder<PaymentAuditEntry> b)
    {
        b.ToTable("payment_audit_entries");
        b.HasIndex(e => new { e.OrderId, e.CreatedAt });
        b.HasIndex(e => e.PaymentId);
        b.HasIndex(e => new { e.EventType, e.CreatedAt });
        b.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        b.Property(e => e.EventType).HasConversion<string>().HasMaxLength(24);
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.Currency).HasMaxLength(8);
        b.Property(e => e.RawPayloadHash).HasMaxLength(64);
        b.Property(e => e.IpAddress).HasMaxLength(64);
        b.Property(e => e.Note).HasMaxLength(500);

        // Append-only at the EF level: any modification of a persisted row throws. Deletes are rejected by
        // PaymentAuditImmutabilityInterceptor (no DB triggers by design).
        foreach (var p in b.Metadata.GetProperties().Where(p => !p.IsPrimaryKey()))
            p.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}

public class PaymentCallbackLogConfiguration : IEntityTypeConfiguration<PaymentCallbackLog>
{
    public void Configure(EntityTypeBuilder<PaymentCallbackLog> b)
    {
        b.ToTable("payment_callback_logs");
        b.HasIndex(l => new { l.Provider, l.TransactionId, l.EventType }).IsUnique();
        b.HasIndex(l => l.OrderId);
        b.Property(l => l.Provider).HasMaxLength(32).IsRequired();
        b.Property(l => l.TransactionId).HasMaxLength(128).IsRequired();
        b.Property(l => l.EventType).HasMaxLength(64).IsRequired();
        b.Property(l => l.Outcome).HasMaxLength(48);
        b.Property(l => l.Result).HasMaxLength(24);
    }
}
