using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Messaging;

namespace Pawzaroo.Infrastructure.Messaging;

public class DeadLetterRecordConfiguration : IEntityTypeConfiguration<DeadLetterRecord>
{
    public void Configure(EntityTypeBuilder<DeadLetterRecord> b)
    {
        b.ToTable("dead_letter_records");
        b.HasIndex(x => new { x.Status, x.FirstFailedAt });
        b.HasIndex(x => new { x.Topic, x.Partition, x.Offset, x.ConsumerGroup });
        b.Property(x => x.Topic).HasMaxLength(256).IsRequired();
        b.Property(x => x.Key).HasMaxLength(512);
        b.Property(x => x.Payload).HasColumnType("text").IsRequired();
        b.Property(x => x.HeadersJson).HasColumnType("text");
        b.Property(x => x.ConsumerGroup).HasMaxLength(128).IsRequired();
        b.Property(x => x.Error).HasMaxLength(4000).IsRequired();
        b.Property(x => x.FailureKind).HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
    }
}
