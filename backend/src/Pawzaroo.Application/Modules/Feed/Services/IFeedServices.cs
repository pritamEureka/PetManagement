using Pawzaroo.Application.Modules.Feed.Dtos;

namespace Pawzaroo.Application.Modules.Feed.Services;

public enum FeedScope { Public, Following, User, Saved, Mine }

public record FeedQuery(
    FeedScope Scope = FeedScope.Public,
    string? Cursor = null,
    int PageSize = 20,
    string? AnimalType = null,
    string? Hashtag = null,
    Guid? UserId = null);

public interface IFeedService
{
    Task<CursorPage<FeedItemDto>> GetFeedAsync(FeedQuery query, CancellationToken ct = default);
    Task<FeedItemDto?> GetByIdAsync(Guid postId, CancellationToken ct = default);

    Task<Guid> CreateAsync(CreatePostInput input, CancellationToken ct = default);
    Task UpdateAsync(Guid postId, UpdatePostInput input, CancellationToken ct = default);
    Task DeleteAsync(Guid postId, CancellationToken ct = default);

    /// <summary>Mod-only soft-hide. Hidden posts stay in DB but disappear from feeds.</summary>
    Task SetHiddenAsync(Guid postId, bool hidden, string? reason, CancellationToken ct = default);
}

public interface IFeedReactionService
{
    Task SetReactionAsync(Guid postId, string type, CancellationToken ct = default);
    Task RemoveReactionAsync(Guid postId, CancellationToken ct = default);
}

public interface IFeedCommentService
{
    Task<CursorPage<CommentDto>> ListAsync(Guid postId, string? cursor, int pageSize, CancellationToken ct = default);
    Task<CommentDto> AddAsync(Guid postId, string content, Guid? parentCommentId, CancellationToken ct = default);
    Task<CommentDto> EditAsync(Guid commentId, string content, CancellationToken ct = default);
    Task DeleteAsync(Guid commentId, CancellationToken ct = default);
}

public interface IFeedSavedPostService
{
    /// <summary>Returns true when the post is now saved, false when it was unsaved.</summary>
    Task<bool> ToggleAsync(Guid postId, CancellationToken ct = default);
    Task<CursorPage<FeedItemDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default);
}

public interface IFeedReportService
{
    Task ReportPostAsync(Guid postId, string reason, string? details, CancellationToken ct = default);
    Task ReportCommentAsync(Guid commentId, string reason, string? details, CancellationToken ct = default);
}

public interface IFeedShareService
{
    Task ShareAsync(Guid postId, string? note, CancellationToken ct = default);
}
