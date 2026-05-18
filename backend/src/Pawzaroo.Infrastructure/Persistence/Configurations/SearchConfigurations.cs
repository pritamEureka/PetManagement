using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Domain.Notifications;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

// Full-text-search via PostgreSQL tsvector was previously configured here on
// Post and Product. Npgsql's HasGeneratedTsVectorColumn requires a real CLR
// property accessor (t => t.SearchVector), not an EF.Property<T> shadow
// lookup — model-building threw at startup. Search currently falls back to
// LIKE/ILIKE in the service layer.
//
// To re-enable: add `public NpgsqlTsVector? SearchVector { get; set; }` to
// Post / Product, then bring back the configuration here using that property.

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> e)
    {
        e.ToTable("audit_entries");
        e.HasIndex(x => x.At);
        e.HasIndex(x => new { x.EntityName, x.EntityId });
        e.Property(x => x.Action).HasMaxLength(64).IsRequired();
        e.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
        e.Property(x => x.EntityId).HasMaxLength(64);
        e.Property(x => x.Module).HasMaxLength(64);
        e.Property(x => x.IpAddress).HasMaxLength(64);
        e.Property(x => x.UserAgent).HasMaxLength(512);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    public void Configure(EntityTypeBuilder<InAppNotification> e)
    {
        e.ToTable("notifications");
        e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
        e.Property(x => x.Title).HasMaxLength(256).IsRequired();
        e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        e.Property(x => x.Url).HasMaxLength(1024);
    }
}
