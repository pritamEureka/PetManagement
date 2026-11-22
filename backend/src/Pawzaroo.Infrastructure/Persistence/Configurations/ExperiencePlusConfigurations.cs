using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Notifications;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class NotificationMuteConfiguration : IEntityTypeConfiguration<NotificationMute>
{
    public void Configure(EntityTypeBuilder<NotificationMute> e)
    {
        e.ToTable("notification_mutes");
        e.Property(x => x.ScopeId).HasMaxLength(100).IsRequired();
        e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.UserId, x.Scope, x.ScopeId });
    }
}

public class NotificationDigestPreferenceConfiguration : IEntityTypeConfiguration<NotificationDigestPreference>
{
    public void Configure(EntityTypeBuilder<NotificationDigestPreference> e)
    {
        e.ToTable("notification_digest_preferences");
        e.Property(x => x.Category).HasMaxLength(50).IsRequired();
        e.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.UserId, x.Category }).IsUnique();
    }
}
