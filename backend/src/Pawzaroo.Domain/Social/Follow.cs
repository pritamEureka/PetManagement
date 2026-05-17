using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Social;

/// <summary>
/// Directed follow edge. (FollowerId, FollowedId) is unique. The following-feed
/// query walks Followed -> Posts.
/// </summary>
public class Follow : BaseEntity
{
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = default!;
    public Guid FollowedId { get; set; }
    public User Followed { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
