namespace Pawzaroo.Application.Modules.Feed.Events;

public static class FeedTopics
{
    public const string Posts        = "pawzaroo.post.events";
    public const string Comments     = "pawzaroo.comment.events";
    public const string Moderation   = "pawzaroo.moderation.events";
}

public record PostCreated(Guid PostId, Guid AuthorId, DateTime At);
public record PostUpdated(Guid PostId, Guid AuthorId, DateTime At);
public record PostDeleted(Guid PostId, Guid AuthorId, DateTime At, bool ByModerator);
public record PostReacted(Guid PostId, Guid UserId, string Type, bool Removed, DateTime At);
public record PostShared(Guid PostId, Guid UserId, DateTime At);
public record PostSavedEvt(Guid PostId, Guid UserId, bool Saved, DateTime At);
public record CommentAdded(Guid CommentId, Guid PostId, Guid AuthorId, Guid? ParentCommentId, DateTime At);
public record CommentEdited(Guid CommentId, Guid AuthorId, DateTime At);
public record CommentDeleted(Guid CommentId, Guid AuthorId, DateTime At);
public record PostReported(Guid PostId, Guid ReporterId, string Reason, DateTime At);
public record CommentReported(Guid CommentId, Guid ReporterId, string Reason, DateTime At);
public record PostModerated(Guid PostId, Guid ActorId, string Action, string? Reason, DateTime At);
