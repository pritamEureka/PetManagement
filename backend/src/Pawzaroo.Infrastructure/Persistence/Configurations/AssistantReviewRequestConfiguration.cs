using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Assistant;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class AssistantReviewRequestConfiguration : IEntityTypeConfiguration<AssistantReviewRequest>
{
    public void Configure(EntityTypeBuilder<AssistantReviewRequest> e)
    {
        e.ToTable("assistant_review_requests");
        e.Property(x => x.Urgency).HasMaxLength(16).IsRequired();
        e.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        e.Property(x => x.ChecklistJson).HasColumnType("jsonb");
        e.Property(x => x.ImageUrlsJson).HasColumnType("jsonb");
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        e.Property(x => x.VetNotes).HasMaxLength(4000);
        e.HasIndex(x => new { x.Status, x.CreatedAt });
        e.HasIndex(x => x.UserId);
    }
}
