using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Domain.Notifications;
using NpgsqlTypes;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

/// <summary>
/// PostgreSQL full-text-search columns. Provider builds a tsvector column and
/// GIN index. Query like: db.Posts.Where(p => p.SearchVector.Matches(query)).
/// </summary>
public class PostSearchConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> e)
    {
        e.HasGeneratedTsVectorColumn(
                p => EF.Property<NpgsqlTsVector>(p, "search_vector"),
                "english",
                p => new { p.Content, p.Location })
            .HasIndex("search_vector")
            .HasMethod("GIN");
    }
}

public class ProductSearchConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.HasGeneratedTsVectorColumn(
                p => EF.Property<NpgsqlTsVector>(p, "search_vector"),
                "english",
                p => new { p.Name, p.Description })
            .HasIndex("search_vector")
            .HasMethod("GIN");
    }
}

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
