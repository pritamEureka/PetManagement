using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Social;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> e)
    {
        e.ToTable("follows");
        e.HasIndex(x => new { x.FollowerId, x.FollowedId }).IsUnique();
        e.HasIndex(x => x.FollowedId);
        e.HasOne(x => x.Follower).WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Followed).WithMany().HasForeignKey(x => x.FollowedId).OnDelete(DeleteBehavior.Cascade);
    }
}
