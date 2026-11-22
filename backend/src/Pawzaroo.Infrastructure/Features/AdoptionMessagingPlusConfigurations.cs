using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Messaging;

namespace Pawzaroo.Infrastructure.Features;

// Entities here have no DbSet on ApplicationDbContext; services use db.Set<T>().

public class AdoptionRequestStageEventConfiguration : IEntityTypeConfiguration<AdoptionRequestStageEvent>
{
    public void Configure(EntityTypeBuilder<AdoptionRequestStageEvent> e)
    {
        e.ToTable("adoption_request_stage_events");
        e.Property(x => x.Note).HasMaxLength(2000);
        e.Property(x => x.Stage).HasConversion<int>();
        e.HasIndex(x => new { x.RequestId, x.EnteredAt });
        e.HasIndex(x => new { x.Stage, x.DueAt });
        e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdoptionReferenceConfiguration : IEntityTypeConfiguration<AdoptionReference>
{
    public void Configure(EntityTypeBuilder<AdoptionReference> e)
    {
        e.ToTable("adoption_references");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Contact).HasMaxLength(300).IsRequired();
        e.Property(x => x.Relationship).HasMaxLength(100);
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.HasIndex(x => x.RequestId);
        e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdoptionHomeVisitConfiguration : IEntityTypeConfiguration<AdoptionHomeVisit>
{
    public void Configure(EntityTypeBuilder<AdoptionHomeVisit> e)
    {
        e.ToTable("adoption_home_visits");
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.Property(x => x.Outcome).HasMaxLength(1000);
        e.HasIndex(x => x.RequestId);
        e.HasOne(x => x.Request).WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdoptionFollowUpConfiguration : IEntityTypeConfiguration<AdoptionFollowUp>
{
    public void Configure(EntityTypeBuilder<AdoptionFollowUp> e)
    {
        e.ToTable("adoption_follow_ups");
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.HasIndex(x => new { x.ListingId, x.DayOffset }).IsUnique();
        e.HasIndex(x => new { x.Status, x.DueAt });
        e.HasIndex(x => x.AdopterId);
        e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FosterPlacementConfiguration : IEntityTypeConfiguration<FosterPlacement>
{
    public void Configure(EntityTypeBuilder<FosterPlacement> e)
    {
        e.ToTable("foster_placements");
        e.Property(x => x.Notes).HasMaxLength(2000);
        e.HasIndex(x => x.ListingId);
        e.HasIndex(x => x.FosterUserId);
        e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdopterProfileConfiguration : IEntityTypeConfiguration<AdopterProfile>
{
    public void Configure(EntityTypeBuilder<AdopterProfile> e)
    {
        e.ToTable("adopter_profiles");
        e.Property(x => x.ChildrenAges).HasMaxLength(200);
        e.Property(x => x.ExistingPetsJson).HasMaxLength(4000);
        e.HasIndex(x => x.UserId).IsUnique();
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdoptionListingRiskFlagConfiguration : IEntityTypeConfiguration<AdoptionListingRiskFlag>
{
    public void Configure(EntityTypeBuilder<AdoptionListingRiskFlag> e)
    {
        e.ToTable("adoption_listing_risk_flags");
        e.Property(x => x.Detail).HasMaxLength(1000).IsRequired();
        e.HasIndex(x => x.ListingId);
        e.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConversationPinConfiguration : IEntityTypeConfiguration<ConversationPin>
{
    public void Configure(EntityTypeBuilder<ConversationPin> e)
    {
        e.ToTable("conversation_pins");
        e.HasIndex(x => new { x.ConversationId, x.MessageId }).IsUnique();
    }
}

public class ConversationFolderConfiguration : IEntityTypeConfiguration<ConversationFolder>
{
    public void Configure(EntityTypeBuilder<ConversationFolder> e)
    {
        e.ToTable("conversation_folders");
        e.Property(x => x.Name).HasMaxLength(80).IsRequired();
        e.HasIndex(x => x.UserId);
    }
}

public class ConversationFolderItemConfiguration : IEntityTypeConfiguration<ConversationFolderItem>
{
    public void Configure(EntityTypeBuilder<ConversationFolderItem> e)
    {
        e.ToTable("conversation_folder_items");
        e.HasIndex(x => new { x.FolderId, x.ConversationId }).IsUnique();
        e.HasOne(x => x.Folder).WithMany().HasForeignKey(x => x.FolderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConversationSettingsConfiguration : IEntityTypeConfiguration<ConversationSettings>
{
    public void Configure(EntityTypeBuilder<ConversationSettings> e)
    {
        e.ToTable("conversation_settings");
        e.HasIndex(x => x.ConversationId).IsUnique();
    }
}

public class MessageEditConfiguration : IEntityTypeConfiguration<MessageEdit>
{
    public void Configure(EntityTypeBuilder<MessageEdit> e)
    {
        e.ToTable("message_edits");
        e.HasIndex(x => x.MessageId);
    }
}

public class UserE2eeKeyConfiguration : IEntityTypeConfiguration<UserE2eeKey>
{
    public void Configure(EntityTypeBuilder<UserE2eeKey> e)
    {
        e.ToTable("user_e2ee_keys");
        e.Property(x => x.PublicKeyJwk).HasMaxLength(2000).IsRequired();
        e.HasIndex(x => x.UserId);
    }
}
