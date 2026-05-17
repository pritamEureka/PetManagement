namespace Pawzaroo.Application.Modules.Feed.Dtos;

public record FeedAuthorDto(Guid Id, string DisplayName, string? AvatarUrl);
public record FeedMediaDto(string Url, string MediaType);

public record FeedItemDto(
    Guid Id,
    string? Content,
    string? AnimalType,
    string? Location,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    FeedAuthorDto Author,
    IReadOnlyList<FeedMediaDto> Media,
    IReadOnlyList<string> Hashtags,
    int ReactionCount,
    int CommentCount,
    int ShareCount,
    string? MyReaction,
    bool IsSaved,
    bool IsOwn,
    bool IsHidden);

public record CommentDto(
    Guid Id,
    Guid PostId,
    Guid? ParentCommentId,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsOwn);

public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public record CreatePostInput(
    string? Content,
    string? AnimalType,
    string? Location,
    IReadOnlyList<string>? MediaUrls,
    IReadOnlyList<string>? Hashtags,
    IReadOnlyList<Guid>? PetTagIds);

public record UpdatePostInput(
    string? Content,
    string? AnimalType,
    string? Location,
    IReadOnlyList<string>? Hashtags);
