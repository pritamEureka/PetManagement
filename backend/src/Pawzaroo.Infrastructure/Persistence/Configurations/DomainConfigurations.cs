using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> e)
    {
        e.ToTable("pets");
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Breed).HasMaxLength(128);
        e.Property(x => x.Color).HasMaxLength(64);
        e.Property(x => x.TagNumber).HasMaxLength(64);
        e.Property(x => x.WeightKg).HasPrecision(8, 2);
        e.HasOne(x => x.Owner).WithMany(u => u.Pets).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> e)
    {
        e.ToTable("posts");
        e.Property(x => x.Content).HasMaxLength(5000);
        e.Property(x => x.Location).HasMaxLength(256);
        e.HasOne(x => x.Author).WithMany(u => u.Posts).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.CreatedAt);
    }
}

public class PostHashtagConfiguration : IEntityTypeConfiguration<PostHashtag>
{
    public void Configure(EntityTypeBuilder<PostHashtag> e)
    {
        e.ToTable("post_hashtags");
        e.HasKey(x => new { x.PostId, x.HashtagId });
    }
}

public class PostPetTagConfiguration : IEntityTypeConfiguration<PostPetTag>
{
    public void Configure(EntityTypeBuilder<PostPetTag> e)
    {
        e.ToTable("post_pet_tags");
        e.HasKey(x => new { x.PostId, x.PetId });
    }
}

public class HashtagConfiguration : IEntityTypeConfiguration<Hashtag>
{
    public void Configure(EntityTypeBuilder<Hashtag> e)
    {
        e.ToTable("hashtags");
        e.HasIndex(x => x.Tag).IsUnique();
        e.Property(x => x.Tag).HasMaxLength(64).IsRequired();
    }
}

public class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> e)
    {
        e.ToTable("reactions");
        e.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> e)
    {
        e.ToTable("comments");
        e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        e.HasOne(x => x.ParentComment).WithMany(c => c.Replies).HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdoptionListingConfiguration : IEntityTypeConfiguration<AdoptionListing>
{
    public void Configure(EntityTypeBuilder<AdoptionListing> e)
    {
        e.ToTable("adoption_listings");
        e.Property(x => x.Title).HasMaxLength(256).IsRequired();
        e.Property(x => x.AdoptionFee).HasPrecision(12, 2);
        e.HasOne(x => x.Owner).WithMany(u => u.AdoptionListings).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Pet).WithMany().HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> e)
    {
        e.ToTable("conversations");
        e.Property(x => x.Title).HasMaxLength(256);
        e.Property(x => x.ContextType).HasMaxLength(32);
    }
}

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> e)
    {
        e.ToTable("conversation_participants");
        e.HasIndex(x => new { x.ConversationId, x.UserId }).IsUnique();
        e.HasOne(x => x.Conversation).WithMany(c => c.Participants).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany(u => u.Conversations).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> e)
    {
        e.ToTable("messages");
        e.Property(x => x.Content).HasMaxLength(4000);
        e.Property(x => x.MediaUrl).HasMaxLength(1024);
        e.HasOne(x => x.Conversation).WithMany(c => c.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ReplyToMessage).WithMany().HasForeignKey(x => x.ReplyToMessageId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
    }
}

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> e)
    {
        e.ToTable("user_blocks");
        e.HasIndex(x => new { x.BlockerId, x.BlockedUserId }).IsUnique();
        e.HasOne(x => x.Blocker).WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.BlockedUser).WithMany().HasForeignKey(x => x.BlockedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> e)
    {
        e.ToTable("doctors");
        e.HasIndex(x => x.UserId).IsUnique();
        e.HasIndex(x => x.LicenseNumber).IsUnique();
        e.Property(x => x.LicenseNumber).HasMaxLength(64).IsRequired();
        e.Property(x => x.Specialty).HasMaxLength(128);
        e.Property(x => x.ConsultationFee).HasPrecision(12, 2);
        e.Property(x => x.RatingAverage).HasPrecision(3, 2);
        e.HasOne(x => x.User).WithOne(u => u.DoctorProfile).HasForeignKey<Doctor>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DoctorAnimalTypeConfiguration : IEntityTypeConfiguration<DoctorAnimalType>
{
    public void Configure(EntityTypeBuilder<DoctorAnimalType> e)
    {
        e.ToTable("doctor_animal_types");
        e.HasKey(x => new { x.DoctorId, x.AnimalType });
    }
}

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> e)
    {
        e.ToTable("appointments");
        e.Property(x => x.Amount).HasPrecision(12, 2);
        e.HasIndex(x => new { x.DoctorId, x.ScheduledAt });
        e.HasOne(x => x.FollowUpOf).WithMany().HasForeignKey(x => x.FollowUpOfAppointmentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pet).WithMany(p => p.Appointments).HasForeignKey(x => x.PetId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> e)
    {
        e.ToTable("stores");
        e.HasIndex(x => x.OwnerUserId).IsUnique();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.CommissionPercent).HasPrecision(5, 2);
        e.HasOne(x => x.OwnerUser).WithOne(u => u.StoreProfile).HasForeignKey<Store>(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("products");
        e.HasIndex(x => x.Sku).IsUnique();
        e.Property(x => x.Name).HasMaxLength(256).IsRequired();
        e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
        e.Property(x => x.Price).HasPrecision(12, 2);
        e.Property(x => x.DiscountPrice).HasPrecision(12, 2);
        e.Property(x => x.RatingAverage).HasPrecision(3, 2);
    }
}

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> e)
    {
        e.ToTable("product_categories");
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
        e.HasOne(x => x.ParentCategory).WithMany(c => c.SubCategories).HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("orders");
        e.HasIndex(x => x.OrderNumber).IsUnique();
        e.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
        e.Property(x => x.Subtotal).HasPrecision(12, 2);
        e.Property(x => x.ShippingFee).HasPrecision(12, 2);
        e.Property(x => x.Tax).HasPrecision(12, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> e)
    {
        e.ToTable("order_items");
        e.Property(x => x.UnitPrice).HasPrecision(12, 2);
        e.Property(x => x.Total).HasPrecision(12, 2);
        e.Property(x => x.CommissionAmount).HasPrecision(12, 2);
        e.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.ToTable("payments");
        e.Property(x => x.Amount).HasPrecision(12, 2);
        e.Property(x => x.Method).HasMaxLength(32);
        e.Property(x => x.TransactionRef).HasMaxLength(128);
    }
}

public class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProviderProfile>
{
    public void Configure(EntityTypeBuilder<ServiceProviderProfile> e)
    {
        e.ToTable("service_providers");
        e.Property(x => x.BusinessName).HasMaxLength(256).IsRequired();
        e.Property(x => x.BasePrice).HasPrecision(12, 2);
        e.Property(x => x.RatingAverage).HasPrecision(3, 2);
    }
}

public class ServiceBookingConfiguration : IEntityTypeConfiguration<ServiceBooking>
{
    public void Configure(EntityTypeBuilder<ServiceBooking> e)
    {
        e.ToTable("service_bookings");
        e.Property(x => x.Amount).HasPrecision(12, 2);
        e.HasIndex(x => new { x.ServiceProviderId, x.ScheduledAt });
    }
}
