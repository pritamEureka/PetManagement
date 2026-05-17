using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Adoption;

namespace Pawzaroo.Infrastructure.Modules.Adoption;

public class AdoptionListingExtendedConfiguration : IEntityTypeConfiguration<AdoptionListing>
{
    public void Configure(EntityTypeBuilder<AdoptionListing> e)
    {
        // Core table already configured in DomainConfigurations.AdoptionListingConfiguration.
        // This file adds the indexes + new-field constraints introduced for the
        // full Adoption module.
        e.Property(x => x.PetName).HasMaxLength(128);
        e.Property(x => x.Color).HasMaxLength(64);
        e.Property(x => x.BehaviorNotes).HasMaxLength(2000);
        e.Property(x => x.ReasonForListing).HasMaxLength(2000);
        e.Property(x => x.AdminNotes).HasMaxLength(2000);

        e.HasIndex(x => x.Status);
        e.HasIndex(x => new { x.Status, x.CreatedAt });
        e.HasIndex(x => new { x.AnimalType, x.Status });
        e.HasIndex(x => x.OwnerId);

        e.HasOne(x => x.DecidedByUser).WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.AdoptedByUser).WithMany().HasForeignKey(x => x.AdoptedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AdoptionRequestConfiguration : IEntityTypeConfiguration<AdoptionRequest>
{
    public void Configure(EntityTypeBuilder<AdoptionRequest> e)
    {
        e.ToTable("adoption_requests");
        e.HasIndex(x => new { x.AdoptionListingId, x.Status });
        e.HasIndex(x => x.RequesterId);
        e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        e.HasOne(x => x.AdoptionListing).WithMany(l => l.Requests).HasForeignKey(x => x.AdoptionListingId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdoptionWantedPostConfiguration : IEntityTypeConfiguration<AdoptionWantedPost>
{
    public void Configure(EntityTypeBuilder<AdoptionWantedPost> e)
    {
        e.ToTable("adoption_wanted_posts");
        e.HasIndex(x => x.RequesterId);
        e.HasIndex(x => new { x.Status, x.CreatedAt });
        e.Property(x => x.Breed).HasMaxLength(128);
        e.Property(x => x.PreferredLocation).HasMaxLength(256);
        e.Property(x => x.ExperienceWithPets).HasMaxLength(2000);
        e.Property(x => x.OtherPetsAtHome).HasMaxLength(1000);
        e.Property(x => x.ReasonForAdoption).HasMaxLength(2000);
        e.Property(x => x.Description).HasMaxLength(4000);
        e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SavedAdoptionListingConfiguration : IEntityTypeConfiguration<SavedAdoptionListing>
{
    public void Configure(EntityTypeBuilder<SavedAdoptionListing> e)
    {
        e.ToTable("saved_adoption_listings");
        e.HasIndex(x => new { x.UserId, x.AdoptionListingId }).IsUnique();
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.AdoptionListing).WithMany().HasForeignKey(x => x.AdoptionListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AdoptionListingPhotoConfiguration : IEntityTypeConfiguration<AdoptionListingPhoto>
{
    public void Configure(EntityTypeBuilder<AdoptionListingPhoto> e)
    {
        e.ToTable("adoption_listing_photos");
        e.HasIndex(x => new { x.AdoptionListingId, x.OrderIndex });
        e.Property(x => x.Url).HasMaxLength(1024).IsRequired();
    }
}
