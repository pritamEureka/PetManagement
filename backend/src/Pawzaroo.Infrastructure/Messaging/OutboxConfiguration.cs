using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Messaging;

namespace Pawzaroo.Infrastructure.Messaging;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        // The dispatcher scans by (status, NextAttemptAt). A composite index
        // keeps that scan cheap even with millions of rows.
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => x.OccurredAt);
        b.Property(x => x.Topic).HasMaxLength(128).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.Version).HasMaxLength(16).IsRequired();
        b.Property(x => x.PartitionKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.Payload).HasColumnType("text").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(128);
    }
}
