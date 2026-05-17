using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> e)
    {
        e.ToTable("user_profiles");
        e.HasIndex(x => x.UserId).IsUnique();
        e.HasOne(x => x.User).WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.FirstName).HasMaxLength(128);
        e.Property(x => x.LastName).HasMaxLength(128);
        e.Property(x => x.Gender).HasMaxLength(32);
        e.Property(x => x.AddressLine1).HasMaxLength(256);
        e.Property(x => x.AddressLine2).HasMaxLength(256);
        e.Property(x => x.City).HasMaxLength(128);
        e.Property(x => x.StateRegion).HasMaxLength(128);
        e.Property(x => x.PostalCode).HasMaxLength(32);
        e.Property(x => x.Country).HasMaxLength(64);
        e.Property(x => x.Website).HasMaxLength(512);
        e.Property(x => x.FacebookHandle).HasMaxLength(128);
        e.Property(x => x.InstagramHandle).HasMaxLength(128);
        e.Property(x => x.TwitterHandle).HasMaxLength(128);
        e.Property(x => x.PreferredLanguage).HasMaxLength(8);
        e.Property(x => x.PreferredCurrency).HasMaxLength(8);
        e.Property(x => x.PreferencesJson).HasColumnType("jsonb");
    }
}

public class CommentReactionConfiguration : IEntityTypeConfiguration<CommentReaction>
{
    public void Configure(EntityTypeBuilder<CommentReaction> e)
    {
        e.ToTable("comment_reactions");
        e.HasIndex(x => new { x.CommentId, x.UserId }).IsUnique();
        e.HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> e)
    {
        e.ToTable("message_attachments");
        e.HasIndex(x => x.MessageId);
        e.Property(x => x.Url).HasMaxLength(1024).IsRequired();
        e.Property(x => x.MimeType).HasMaxLength(128).IsRequired();
        e.Property(x => x.FileName).HasMaxLength(256);
        e.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> e)
    {
        e.ToTable("content_reports");
        e.HasIndex(x => new { x.TargetType, x.TargetId });
        e.HasIndex(x => new { x.Status, x.CreatedAt });
        e.Property(x => x.Reason).HasMaxLength(128).IsRequired();
        e.Property(x => x.Details).HasMaxLength(2000);
        e.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SpecialtyConfiguration : IEntityTypeConfiguration<Specialty>
{
    public void Configure(EntityTypeBuilder<Specialty> e)
    {
        e.ToTable("specialties");
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Name).HasMaxLength(128).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(128).IsRequired();
    }
}

public class DoctorSpecialtyConfiguration : IEntityTypeConfiguration<DoctorSpecialty>
{
    public void Configure(EntityTypeBuilder<DoctorSpecialty> e)
    {
        e.ToTable("doctor_specialties");
        e.HasKey(x => new { x.DoctorId, x.SpecialtyId });
        e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Specialty).WithMany(s => s.Doctors).HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorTimeSlotConfiguration : IEntityTypeConfiguration<DoctorTimeSlot>
{
    public void Configure(EntityTypeBuilder<DoctorTimeSlot> e)
    {
        e.ToTable("doctor_time_slots");
        e.HasIndex(x => new { x.DoctorId, x.StartUtc });
        e.HasIndex(x => new { x.DoctorId, x.Status, x.StartUtc });
        e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> e)
    {
        e.ToTable("prescriptions");
        e.HasIndex(x => x.AppointmentId);
        e.HasIndex(x => x.IssuedById);
        e.Property(x => x.Notes).HasMaxLength(4000);
        e.Property(x => x.FileUrl).HasMaxLength(1024);
        e.Property(x => x.ItemsJson).HasColumnType("jsonb");
        e.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.IssuedBy).WithMany().HasForeignKey(x => x.IssuedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> e)
    {
        e.ToTable("carts");
        e.HasIndex(x => new { x.UserId, x.Status });
        e.Property(x => x.Currency).HasMaxLength(8);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemRelationConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> e)
    {
        e.ToTable("cart_items");
        e.HasIndex(x => x.CartId);
        e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
        e.Property(x => x.UnitPriceSnapshot).HasPrecision(12, 2);
        e.HasOne(x => x.Cart).WithMany(c => c.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> e)
    {
        e.ToTable("inventory_adjustments");
        e.HasIndex(x => new { x.ProductId, x.CreatedAt });
        e.HasIndex(x => x.OrderId);
        e.Property(x => x.Notes).HasMaxLength(512);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> e)
    {
        e.ToTable("approval_requests");
        e.HasIndex(x => new { x.EntityType, x.EntityId });
        e.HasIndex(x => new { x.Decision, x.CreatedAt });
        e.Property(x => x.AdminNotes).HasMaxLength(2000);
        e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> e)
    {
        e.ToTable("system_settings");
        e.HasIndex(x => x.Key).IsUnique();
        e.HasIndex(x => x.Category);
        e.Property(x => x.Key).HasMaxLength(128).IsRequired();
        e.Property(x => x.Category).HasMaxLength(64).IsRequired();
        e.Property(x => x.Description).HasMaxLength(512);
        e.Property(x => x.ValueJson).HasColumnType("jsonb").IsRequired();
    }
}
