using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Social;

public class CommentReaction : BaseEntity
{
    public Guid CommentId { get; set; }
    public Comment Comment { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public ReactionType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
