using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedCommentService : IFeedCommentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IFeedCache _cache;

    public FeedCommentService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IFeedCache cache)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _cache = cache;
    }

    private bool IsModerator => _current.Permissions.Contains(Permissions.Comments.Moderate)
                                || _current.Permissions.Contains(Permissions.Comments.Delete);

    public async Task<CursorPage<CommentDto>> ListAsync(Guid postId, string? cursor, int pageSize, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var q = _db.Comments.AsNoTracking().Where(c => c.PostId == postId && c.ParentCommentId == null);

        var cur = FeedCursor.Decode(cursor);
        if (cur is { } c) q = q.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(pageSize, 1, 100);
        var rows = await q.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Take(take + 1)
            .Select(c => new CommentDto(
                c.Id, c.PostId, c.ParentCommentId, c.AuthorId,
                c.Author.DisplayName, c.Author.AvatarUrl,
                c.Content, c.CreatedAt, c.UpdatedAt,
                uid != null && c.AuthorId == uid))
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = FeedCursor.Encode(last.CreatedAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }
        return new CursorPage<CommentDto>(rows, next);
    }

    public async Task<CommentDto> AddAsync(Guid postId, string content, Guid? parentCommentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { ["content"] = new[] { "Required" } });
        var uid = _current.UserId ?? throw new ForbiddenException();

        if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct))
            throw new NotFoundException("Post", postId);

        var c = new Comment { PostId = postId, AuthorId = uid, Content = content.Trim(), ParentCommentId = parentCommentId };
        _db.Comments.Add(c);
        await _db.SaveChangesAsync(ct);

        await _cache.BumpCommentCountAsync(postId, +1, ct);
        await _kafka.PublishAsync(FeedTopics.Comments,
            new CommentAdded(c.Id, postId, uid, parentCommentId, DateTime.UtcNow),
            c.Id.ToString(), ct);

        var author = await _db.Users.Where(u => u.Id == uid)
            .Select(u => new { u.DisplayName, u.AvatarUrl }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", uid);
        return new CommentDto(c.Id, c.PostId, c.ParentCommentId, c.AuthorId,
            author.DisplayName, author.AvatarUrl, c.Content, c.CreatedAt, c.UpdatedAt, true);
    }

    public async Task<CommentDto> EditAsync(Guid commentId, string content, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var c = await _db.Comments.SingleOrDefaultAsync(x => x.Id == commentId, ct)
            ?? throw new NotFoundException("Comment", commentId);
        if (c.AuthorId != uid && !IsModerator) throw new ForbiddenException();

        c.Content = content.Trim();
        await _db.SaveChangesAsync(ct);

        var author = await _db.Users.Where(u => u.Id == c.AuthorId)
            .Select(u => new { u.DisplayName, u.AvatarUrl }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("User", c.AuthorId);
        return new CommentDto(c.Id, c.PostId, c.ParentCommentId, c.AuthorId,
            author.DisplayName, author.AvatarUrl, c.Content, c.CreatedAt, c.UpdatedAt,
            c.AuthorId == uid);
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var c = await _db.Comments.SingleOrDefaultAsync(x => x.Id == commentId, ct)
            ?? throw new NotFoundException("Comment", commentId);
        if (c.AuthorId != uid && !IsModerator) throw new ForbiddenException();

        var postId = c.PostId;
        _db.Comments.Remove(c);
        await _db.SaveChangesAsync(ct);

        await _cache.BumpCommentCountAsync(postId, -1, ct);
        await _kafka.PublishAsync(FeedTopics.Comments,
            new CommentDeleted(commentId, c.AuthorId, DateTime.UtcNow),
            commentId.ToString(), ct);
    }
}
