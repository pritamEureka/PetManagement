using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Features;

public class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> b)
    {
        b.ToTable("delivery_zones");
        b.HasIndex(z => new { z.StoreId, z.IsActive });
        b.Property(z => z.Name).HasMaxLength(120).IsRequired();
        b.Property(z => z.Cities).HasMaxLength(4000);
        b.Property(z => z.BaseFee).HasPrecision(12, 2);
        b.Property(z => z.FreeShippingThreshold).HasPrecision(12, 2);
    }
}

public class PeakHourRuleConfiguration : IEntityTypeConfiguration<PeakHourRule>
{
    public void Configure(EntityTypeBuilder<PeakHourRule> b)
    {
        b.ToTable("peak_hour_rules");
        b.HasIndex(r => new { r.ZoneId, r.IsActive });
        b.Property(r => r.SurchargeFixed).HasPrecision(12, 2);
        b.Property(r => r.SurchargePercent).HasPrecision(7, 2);
        b.Property(r => r.Label).HasMaxLength(120);
    }
}

public class FulfillmentSlaConfiguration : IEntityTypeConfiguration<FulfillmentSla>
{
    public void Configure(EntityTypeBuilder<FulfillmentSla> b)
    {
        b.ToTable("fulfillment_slas");
        b.HasIndex(s => s.StoreId);
        b.Property(s => s.PenaltyNote).HasMaxLength(500);
    }
}

public class SlaBreachConfiguration : IEntityTypeConfiguration<SlaBreach>
{
    public void Configure(EntityTypeBuilder<SlaBreach> b)
    {
        b.ToTable("sla_breaches");
        b.HasIndex(x => new { x.OrderId, x.StoreId, x.Kind }).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.BreachedAt });
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
    }
}

public class ReturnPickupConfiguration : IEntityTypeConfiguration<ReturnPickup>
{
    public void Configure(EntityTypeBuilder<ReturnPickup> b)
    {
        b.ToTable("return_pickups");
        b.HasIndex(p => p.ReturnRequestId).IsUnique();
        b.HasIndex(p => new { p.DeliveryUserId, p.Status });
        b.Property(p => p.AddressSnapshot).HasMaxLength(1000).IsRequired();
        b.Property(p => p.Notes).HasMaxLength(500);
        b.Property(p => p.ProofPhotoUrl).HasMaxLength(1024);
        b.Property(p => p.ProofSignatureName).HasMaxLength(200);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(24);
    }
}

public class ReturnPickupCheckpointConfiguration : IEntityTypeConfiguration<ReturnPickupCheckpoint>
{
    public void Configure(EntityTypeBuilder<ReturnPickupCheckpoint> b)
    {
        b.ToTable("return_pickup_checkpoints");
        b.HasIndex(c => new { c.ReturnPickupId, c.OccurredAt });
        b.Property(c => c.Note).HasMaxLength(500);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(24);
    }
}

public class OrderRefundConfiguration : IEntityTypeConfiguration<OrderRefund>
{
    public void Configure(EntityTypeBuilder<OrderRefund> b)
    {
        b.ToTable("order_refunds");
        b.HasIndex(r => r.OrderId);
        b.HasIndex(r => r.OrderItemId).IsUnique();   // one partial refund per order item (matches ledger idempotency)
        b.Property(r => r.Amount).HasPrecision(18, 2);
        b.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        b.Property(r => r.GatewayStatus).HasMaxLength(32).IsRequired();
        b.Property(r => r.Note).HasMaxLength(500);
    }
}
