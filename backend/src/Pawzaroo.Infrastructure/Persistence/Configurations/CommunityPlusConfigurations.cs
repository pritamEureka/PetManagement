using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Domain.Notifications;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> e)
    {
        e.ToTable("communities");
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        e.Property(x => x.Species).HasMaxLength(60);
        e.Property(x => x.Breed).HasMaxLength(80);
        e.Property(x => x.City).HasMaxLength(100);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(CommunityKind.Community);
        e.HasIndex(x => x.Slug).IsUnique();
        e.HasIndex(x => new { x.Species, x.Breed });
    }
}

public class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    public void Configure(EntityTypeBuilder<CommunityMember> e)
    {
        e.ToTable("community_members");
        e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.CommunityId, x.UserId }).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Status });
    }
}

public class CommunityEventConfiguration : IEntityTypeConfiguration<CommunityEvent>
{
    public void Configure(EntityTypeBuilder<CommunityEvent> e)
    {
        e.ToTable("community_events");
        e.Property(x => x.Title).HasMaxLength(160).IsRequired();
        e.Property(x => x.Description).HasMaxLength(4000);
        e.Property(x => x.City).HasMaxLength(100);
        e.Property(x => x.Venue).HasMaxLength(200);
        e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        e.HasIndex(x => new { x.CommunityId, x.StartsAt });
        e.HasIndex(x => new { x.IsCancelled, x.StartsAt, x.Latitude, x.Longitude });
    }
}

public class EventRsvpConfiguration : IEntityTypeConfiguration<EventRsvp>
{
    public void Configure(EntityTypeBuilder<EventRsvp> e)
    {
        e.ToTable("event_rsvps");
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
        e.HasIndex(x => new { x.EventId, x.Status, x.RsvpAt });
    }
}

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> e)
    {
        e.ToTable("push_subscriptions");
        e.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
        e.Property(x => x.P256dh).HasMaxLength(256).IsRequired();
        e.Property(x => x.Auth).HasMaxLength(128).IsRequired();
        e.Property(x => x.UserAgent).HasMaxLength(400);
        e.HasIndex(x => x.Endpoint).IsUnique();
        e.HasIndex(x => x.UserId);
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> e)
    {
        e.ToTable("notification_preferences");
        e.Property(x => x.Category).HasMaxLength(50).IsRequired();
        e.HasIndex(x => new { x.UserId, x.Category }).IsUnique();
    }
}

public class NotificationQuietHoursConfiguration : IEntityTypeConfiguration<NotificationQuietHours>
{
    public void Configure(EntityTypeBuilder<NotificationQuietHours> e)
    {
        e.ToTable("notification_quiet_hours");
        e.HasIndex(x => x.UserId).IsUnique();
    }
}
