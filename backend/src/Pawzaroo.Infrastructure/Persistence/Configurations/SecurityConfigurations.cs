using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Moderation;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> b)
    {
        b.ToTable("user_devices");
        b.HasIndex(d => new { d.UserId, d.Fingerprint }).IsUnique();
        b.HasIndex(d => d.LastSeenAt);
        b.Property(d => d.Fingerprint).HasMaxLength(128).IsRequired();
        b.Property(d => d.UserAgent).HasMaxLength(512);
        b.Property(d => d.IpAddress).HasMaxLength(64);
        b.Property(d => d.IpCity).HasMaxLength(128);
        b.Property(d => d.IpCountry).HasMaxLength(64);
        b.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSuspensionConfiguration : IEntityTypeConfiguration<UserSuspension>
{
    public void Configure(EntityTypeBuilder<UserSuspension> b)
    {
        b.ToTable("user_suspensions");
        b.HasIndex(s => new { s.UserId, s.Status });
        b.Property(s => s.Reason).HasMaxLength(256).IsRequired();
        b.Property(s => s.Details).HasMaxLength(2000);
        b.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.IssuedBy).WithMany().HasForeignKey(s => s.IssuedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.LiftedBy).WithMany().HasForeignKey(s => s.LiftedById).OnDelete(DeleteBehavior.SetNull);
    }
}

public class UserWarningConfiguration : IEntityTypeConfiguration<UserWarning>
{
    public void Configure(EntityTypeBuilder<UserWarning> b)
    {
        b.ToTable("user_warnings");
        b.HasIndex(w => new { w.UserId, w.AcknowledgedByUser });
        b.Property(w => w.Reason).HasMaxLength(256).IsRequired();
        b.Property(w => w.Message).HasMaxLength(2000);
        b.Property(w => w.RelatedContentType).HasMaxLength(64);
        b.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(w => w.IssuedBy).WithMany().HasForeignKey(w => w.IssuedById).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.ToTable("otp_codes");
        b.HasIndex(o => new { o.UserId, o.Purpose, o.Consumed });
        b.HasIndex(o => o.ExpiresAt);
        b.Property(o => o.CodeHash).HasMaxLength(128).IsRequired();
        b.Property(o => o.Destination).HasMaxLength(256);
        b.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TwoFactorSettingsConfiguration : IEntityTypeConfiguration<TwoFactorSettings>
{
    public void Configure(EntityTypeBuilder<TwoFactorSettings> b)
    {
        b.ToTable("two_factor_settings");
        b.HasIndex(t => t.UserId).IsUnique();
        b.Property(t => t.EncryptedSecret).HasMaxLength(512);
        b.Property(t => t.RecoveryCodesHash).HasMaxLength(2048);
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> b)
    {
        b.ToTable("moderation_actions");
        b.HasIndex(a => new { a.TargetType, a.TargetId });
        b.HasIndex(a => a.ModeratorId);
        b.HasIndex(a => a.CreatedAt);
        b.Property(a => a.Notes).HasMaxLength(2000);
        b.HasOne(a => a.Moderator).WithMany().HasForeignKey(a => a.ModeratorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Report).WithMany().HasForeignKey(a => a.ReportId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(a => a.RelatedSuspension).WithMany().HasForeignKey(a => a.RelatedSuspensionId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(a => a.RelatedWarning).WithMany().HasForeignKey(a => a.RelatedWarningId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    public void Configure(EntityTypeBuilder<AdminActionLog> b)
    {
        b.ToTable("admin_action_logs");
        b.HasIndex(l => new { l.AdminId, l.At });
        b.HasIndex(l => new { l.TargetType, l.TargetId });
        b.Property(l => l.Action).HasMaxLength(128).IsRequired();
        b.Property(l => l.TargetType).HasMaxLength(64).IsRequired();
        b.Property(l => l.TargetId).HasMaxLength(64);
        b.Property(l => l.Reason).HasMaxLength(1000);
        b.Property(l => l.IpAddress).HasMaxLength(64);
        b.Property(l => l.UserAgent).HasMaxLength(512);
        b.HasOne(l => l.Admin).WithMany().HasForeignKey(l => l.AdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
