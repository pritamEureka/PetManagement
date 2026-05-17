using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Audit;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Store;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

/// <summary>
/// Indexes for high-traffic read paths that aren't already declared on the
/// primary entity configuration. Kept in one file so it's obvious where the
/// performance-critical indexes live.
/// </summary>
public class FeedHotIndexes : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> e)
    {
        e.HasIndex(x => new { x.PostId, x.OrderIndex });
    }
}

public class CommentHotIndexes : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> e)
    {
        e.HasIndex(x => new { x.PostId, x.CreatedAt });
        e.HasIndex(x => x.ParentCommentId);
    }
}

public class PostSaveIndexes : IEntityTypeConfiguration<PostSave>
{
    public void Configure(EntityTypeBuilder<PostSave> e)
    {
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasIndex(x => new { x.UserId, x.PostId }).IsUnique();
    }
}

public class NotificationHotIndexes : IEntityTypeConfiguration<InAppNotification>
{
    public void Configure(EntityTypeBuilder<InAppNotification> e)
    {
        // Already declared in NotificationConfiguration; this is a no-op safeguard
        // if that file is ever removed. Keep idempotent.
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

public class AuditHotIndexes : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> e)
    {
        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.Module);
    }
}

public class ProductSearchIndexes : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.HasIndex(x => new { x.StoreId, x.IsActive });
        e.HasIndex(x => new { x.CategoryId, x.IsActive });
        e.HasIndex(x => new { x.IsFeatured, x.CreatedAt });
    }
}
