using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Communities;

/// <summary>Community poll. Moderators may close/delete; closed polls keep their results.</summary>
public class CommunityPoll : AuditableEntity
{
    public Guid CommunityId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Question { get; set; } = default!;
    public bool MultiChoice { get; set; }
    public DateTime? ClosesAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
}

public class PollOption : BaseEntity
{
    public Guid PollId { get; set; }
    public CommunityPoll Poll { get; set; } = default!;
    public string Text { get; set; } = default!;
    public int SortOrder { get; set; }
}

/// <summary>Unique per (user, option). Single-choice is enforced in the service per poll.</summary>
public class PollVote : BaseEntity
{
    public Guid PollId { get; set; }
    public Guid OptionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ReputationKind
{
    PostLiked = 0, CommentLiked = 1, PollCreated = 2, EventHosted = 3, EventAttended = 4,
    HelpfulAnswer = 5, ReportUpheld = 6, Penalty = 7
}

/// <summary>Append-only ledger. (UserId, Kind, SourceId) is unique so awards are idempotent.</summary>
public class ReputationEvent : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? CommunityId { get; set; }
    public ReputationKind Kind { get; set; }
    public int Points { get; set; }
    public Guid SourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Materialised totals. CommunityId null = global row.</summary>
public class UserReputation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? CommunityId { get; set; }
    public int Total { get; set; }
    public int Level { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
