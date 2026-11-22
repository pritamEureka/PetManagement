using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Assistant;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class AssistantConversationConfiguration : IEntityTypeConfiguration<AssistantConversation>
{
    public void Configure(EntityTypeBuilder<AssistantConversation> e)
    {
        e.ToTable("assistant_conversations");
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.HasIndex(x => new { x.UserId, x.LastMessageAt });
    }
}

public class AssistantMessageConfiguration : IEntityTypeConfiguration<AssistantMessage>
{
    public void Configure(EntityTypeBuilder<AssistantMessage> e)
    {
        e.ToTable("assistant_messages");
        e.Property(x => x.Role).HasMaxLength(16).IsRequired();
        e.Property(x => x.Content).HasMaxLength(8000).IsRequired();
        e.Property(x => x.Intent).HasMaxLength(32);
        e.Property(x => x.CitationsJson).HasColumnType("jsonb");
        e.Property(x => x.AttachmentsJson).HasColumnType("jsonb");
        e.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
    }
}
