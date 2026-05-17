using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Messaging;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

public class MessageReadReceiptConfiguration : IEntityTypeConfiguration<MessageReadReceipt>
{
    public void Configure(EntityTypeBuilder<MessageReadReceipt> e)
    {
        e.ToTable("message_read_receipts");
        e.HasIndex(x => new { x.MessageId, x.UserId }).IsUnique();
        e.HasIndex(x => x.UserId);
        e.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
