using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Features;

public class DeliveryCheckpointConfiguration : IEntityTypeConfiguration<DeliveryCheckpoint>
{
    public void Configure(EntityTypeBuilder<DeliveryCheckpoint> b)
    {
        b.ToTable("delivery_checkpoints");
        b.HasIndex(c => new { c.OrderId, c.OccurredAt });
        b.HasIndex(c => new { c.Provider, c.ExternalEventId }).IsUnique()
            .HasFilter("external_event_id IS NOT NULL");
        b.Property(c => c.Description).HasMaxLength(500);
        b.Property(c => c.LocationText).HasMaxLength(300);
        b.Property(c => c.Provider).HasMaxLength(64);
        b.Property(c => c.ExternalEventId).HasMaxLength(128);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(c => c.Source).HasConversion<string>().HasMaxLength(16);
    }
}

public class DeliveryProofConfiguration : IEntityTypeConfiguration<DeliveryProof>
{
    public void Configure(EntityTypeBuilder<DeliveryProof> b)
    {
        b.ToTable("delivery_proofs");
        b.HasIndex(p => p.OrderId).IsUnique();
        b.Property(p => p.PhotoUrl).HasMaxLength(1024);
        b.Property(p => p.SignatureName).HasMaxLength(200);
        b.Property(p => p.OtpCode).HasMaxLength(16);
        b.Property(p => p.RecipientName).HasMaxLength(200);
    }
}

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("ledger_entries");
        b.HasIndex(e => new { e.StoreId, e.CreatedAt });
        b.HasIndex(e => new { e.StoreId, e.AvailableAt });
        b.HasIndex(e => e.OrderId);
        // Idempotency: one entry per (order item, type). Payout entries have no order item.
        b.HasIndex(e => new { e.OrderItemId, e.Type }).IsUnique()
            .HasFilter("order_item_id IS NOT NULL");
        b.Property(e => e.Amount).HasPrecision(18, 2);
        b.Property(e => e.Currency).HasMaxLength(8).IsRequired();
        b.Property(e => e.Memo).HasMaxLength(500);
        b.Property(e => e.Type).HasConversion<string>().HasMaxLength(16);
    }
}

public class SettlementBatchConfiguration : IEntityTypeConfiguration<SettlementBatch>
{
    public void Configure(EntityTypeBuilder<SettlementBatch> b)
    {
        b.ToTable("settlement_batches");
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => x.CreatedAt);
        b.Property(x => x.Reference).HasMaxLength(64).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasMany(x => x.Payouts).WithOne(p => p.SettlementBatch)
            .HasForeignKey(p => p.SettlementBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StorePayoutConfiguration : IEntityTypeConfiguration<StorePayout>
{
    public void Configure(EntityTypeBuilder<StorePayout> b)
    {
        b.ToTable("store_payouts");
        b.HasIndex(p => new { p.SettlementBatchId, p.StoreId }).IsUnique();
        b.HasIndex(p => new { p.StoreId, p.CreatedAt });
        b.Property(p => p.Amount).HasPrecision(18, 2);
        b.Property(p => p.GatewayAmount).HasPrecision(18, 2);
        b.Property(p => p.Currency).HasMaxLength(8).IsRequired();
        b.Property(p => p.GatewayReference).HasMaxLength(128);
        b.Property(p => p.Notes).HasMaxLength(500);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
    }
}

public class StorePayoutLineConfiguration : IEntityTypeConfiguration<StorePayoutLine>
{
    public void Configure(EntityTypeBuilder<StorePayoutLine> b)
    {
        b.ToTable("store_payout_lines");
        // A ledger entry can be settled by at most one payout.
        b.HasIndex(l => l.LedgerEntryId).IsUnique();
        b.HasIndex(l => l.StorePayoutId);
    }
}
