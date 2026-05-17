using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedService : IFeedService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IFeedCache _cache;
    private readonly IAuditLogger _audit;

    public FeedService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IFeedCache cache, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _cache = cache;
        _audit = audit;
    }

    private bool IsModerator => _current.Permissions.Contains(Permissions.Posts.Moderate)
                                || _current.Permissions.Contains(Permissions.Posts.Delete);

    public async Task<CursorPage<FeedItemDto>> GetFeedAsync(FeedQuery query, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        var scopeKey = BuildScopeKey(query, uid);

        // Hot first-page cache: only for the public scope with no filters and no cursor.
        if (query.Scope == FeedScope.Public && query.Cursor is null
            && string.IsNullOrEmpty(query.AnimalType) && string.IsNullOrEmpty(query.Hashtag))
        {
            var cached = await _cache.GetFirstPageAsync<CursorPage<FeedItemDto>>(scopeKey, ct);
            if (cached is not null) return cached;
        }

        var q = _db.Posts.AsNoTracking().Where(p => !p.IsHidden);

        switch (query.Scope)
        {
            case FeedScope.User when query.UserId.HasValue:
                q = q.Where(p => p.AuthorId == query.UserId.Value);
                break;
            case FeedScope.Mine:
                if (uid is null) throw new ForbiddenException();
                q = q.IgnoreQueryFilters().Where(p => p.AuthorId == uid && !p.IsDeleted);
                break;
            case FeedScope.Saved:
                if (uid is null) throw new ForbiddenException();
                q = q.Where(p => _db.PostSaves.Any(s => s.PostId == p.Id && s.UserId == uid));
                break;
            case FeedScope.Following:
                if (uid is null) throw new ForbiddenException();
                q = q.Where(p => _db.Follows.Any(f => f.FollowerId == uid && f.FollowedId == p.AuthorId));
                break;
        }

        if (!string.IsNullOrWhiteSpace(query.AnimalType)
            && Enum.TryParse<AnimalType>(query.AnimalType, true, out var at))
            q = q.Where(p => p.AnimalType == at);

        if (!string.IsNullOrWhiteSpace(query.Hashtag))
        {
            var tag = query.Hashtag.Trim().TrimStart('#').ToLowerInvariant();
            q = q.Where(p => p.Hashtags.Any(h => h.Hashtag.Tag == tag));
        }

        var cur = FeedCursor.Decode(query.Cursor);
        if (cur is { } c)
            q = q.Where(p => p.CreatedAt < c.CreatedAt || (p.CreatedAt == c.CreatedAt && p.Id.CompareTo(c.Id) < 0));

        var take = Math.Clamp(query.PageSize, 1, 50);
        var rows = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(take + 1)
            .Select(p => new
            {
                p.Id, p.Content, p.AnimalType, p.Location, p.CreatedAt, p.UpdatedAt, p.AuthorId, p.IsHidden,
                AuthorName = p.Author.DisplayName, AuthorAvatar = p.Author.AvatarUrl,
                Media = p.Media.OrderBy(m => m.OrderIndex).Select(m => new FeedMediaDto(m.Url, m.MediaType)).ToList(),
                Hashtags = p.Hashtags.Select(h => h.Hashtag.Tag).ToList(),
                ReactionCount = p.Reactions.Count,
                CommentCount = p.Comments.Count,
                ShareCount = p.Shares.Count,
                MyReaction = uid == null ? null : p.Reactions.Where(r => r.UserId == uid).Select(r => r.Type.ToString()).FirstOrDefault(),
                IsSaved = uid != null && _db.PostSaves.Any(s => s.PostId == p.Id && s.UserId == uid),
            })
            .ToListAsync(ct);

        string? next = null;
        if (rows.Count > take)
        {
            var last = rows[take - 1];
            next = FeedCursor.Encode(last.CreatedAt, last.Id);
            rows.RemoveAt(rows.Count - 1);
        }

        var items = rows.Select(r => new FeedItemDto(
            r.Id, r.Content, r.AnimalType?.ToString(), r.Location, r.CreatedAt, r.UpdatedAt,
            new FeedAuthorDto(r.AuthorId, r.AuthorName, r.AuthorAvatar),
            r.Media, r.Hashtags,
            r.ReactionCount, r.CommentCount, r.ShareCount,
            r.MyReaction, r.IsSaved,
            uid != null && r.AuthorId == uid,
            r.IsHidden
        )).ToList();

        var page = new CursorPage<FeedItemDto>(items, next);

        if (query.Scope == FeedScope.Public && query.Cursor is null
            && string.IsNullOrEmpty(query.AnimalType) && string.IsNullOrEmpty(query.Hashtag))
            await _cache.SetFirstPageAsync(scopeKey, page, ct: ct);

        return page;
    }

    public async Task<FeedItemDto?> GetByIdAsync(Guid postId, CancellationToken ct = default)
    {
        var uid = _current.UserId;
        return await _db.Posts.AsNoTracking().Where(p => p.Id == postId)
            .Select(p => new FeedItemDto(
                p.Id, p.Content, p.AnimalType.HasValue ? p.AnimalType.Value.ToString() : null, p.Location, p.CreatedAt, p.UpdatedAt,
                new FeedAuthorDto(p.AuthorId, p.Author.DisplayName, p.Author.AvatarUrl),
                p.Media.OrderBy(m => m.OrderIndex).Select(m => new FeedMediaDto(m.Url, m.MediaType)).ToList(),
                p.Hashtags.Select(h => h.Hashtag.Tag).ToList(),
                p.Reactions.Count, p.Comments.Count, p.Shares.Count,
                uid == null ? null : p.Reactions.Where(r => r.UserId == uid).Select(r => r.Type.ToString()).FirstOrDefault(),
                uid != null && _db.PostSaves.Any(s => s.PostId == p.Id && s.UserId == uid),
                uid != null && p.AuthorId == uid,
                p.IsHidden))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateAsync(CreatePostInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        AnimalType? animalType = ParseAnimal(input.AnimalType);

        var post = new Post
        {
            AuthorId = uid,
            Content = input.Content,
            AnimalType = animalType,
            Location = input.Location
        };
        if (input.MediaUrls is { Count: > 0 })
            for (int i = 0; i < input.MediaUrls.Count; i++)
                post.Media.Add(new PostMedia { Url = input.MediaUrls[i], OrderIndex = i, MediaType = GuessType(input.MediaUrls[i]) });

        if (input.Hashtags is { Count: > 0 })
            await AttachHashtagsAsync(post, input.Hashtags, ct);

        if (input.PetTagIds is { Count: > 0 })
            foreach (var pid in input.PetTagIds.Distinct()) post.PetTags.Add(new PostPetTag { PetId = pid });

        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);

        await _cache.InvalidateFeedFirstPagesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Posts, new PostCreated(post.Id, uid, DateTime.UtcNow), post.Id.ToString(), ct);
        return post.Id;
    }

    public async Task UpdateAsync(Guid postId, UpdatePostInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var post = await _db.Posts.Include(p => p.Hashtags).ThenInclude(h => h.Hashtag)
            .SingleOrDefaultAsync(p => p.Id == postId, ct)
            ?? throw new NotFoundException("Post", postId);
        if (post.AuthorId != uid && !IsModerator) throw new ForbiddenException();

        post.Content = input.Content;
        post.AnimalType = ParseAnimal(input.AnimalType);
        post.Location = input.Location;

        if (input.Hashtags is not null)
        {
            _db.PostHashtags.RemoveRange(post.Hashtags);
            post.Hashtags.Clear();
            if (input.Hashtags.Count > 0) await AttachHashtagsAsync(post, input.Hashtags, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateFeedFirstPagesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Posts, new PostUpdated(post.Id, post.AuthorId, DateTime.UtcNow), post.Id.ToString(), ct);
    }

    public async Task DeleteAsync(Guid postId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var post = await _db.Posts.SingleOrDefaultAsync(p => p.Id == postId, ct)
            ?? throw new NotFoundException("Post", postId);
        var byModerator = post.AuthorId != uid;
        if (byModerator && !IsModerator) throw new ForbiddenException();

        _db.Posts.Remove(post);                  // soft-delete via SaveChanges convention
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateFeedFirstPagesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Posts, new PostDeleted(post.Id, post.AuthorId, DateTime.UtcNow, byModerator), post.Id.ToString(), ct);
    }

    public async Task SetHiddenAsync(Guid postId, bool hidden, string? reason, CancellationToken ct = default)
    {
        if (!IsModerator) throw new ForbiddenException();
        var uid = _current.UserId ?? throw new ForbiddenException();
        var post = await _db.Posts.SingleOrDefaultAsync(p => p.Id == postId, ct)
            ?? throw new NotFoundException("Post", postId);
        post.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
        await _cache.InvalidateFeedFirstPagesAsync(ct);
        await _audit.LogAsync(hidden ? "hide" : "unhide", "Post", postId.ToString(), "Feed",
            newValues: new { reason }, ct: ct);
        await _kafka.PublishAsync(FeedTopics.Moderation,
            new PostModerated(postId, uid, hidden ? "hidden" : "unhidden", reason, DateTime.UtcNow),
            postId.ToString(), ct);
    }

    private static AnimalType? ParseAnimal(string? raw)
        => string.IsNullOrWhiteSpace(raw) || !Enum.TryParse<AnimalType>(raw, true, out var v) ? null : v;

    private static string GuessType(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.EndsWith(".mp4") || lower.EndsWith(".webm") || lower.EndsWith(".mov") ? "video" : "image";
    }

    private async Task AttachHashtagsAsync(Post post, IReadOnlyList<string> tags, CancellationToken ct)
    {
        var normalized = tags.Select(t => t.Trim().TrimStart('#').ToLowerInvariant())
                             .Where(t => !string.IsNullOrEmpty(t))
                             .Distinct().ToArray();
        var existing = await _db.Hashtags.Where(h => normalized.Contains(h.Tag)).ToDictionaryAsync(h => h.Tag, ct);
        foreach (var t in normalized)
        {
            if (!existing.TryGetValue(t, out var tag))
            {
                tag = new Hashtag { Tag = t };
                _db.Hashtags.Add(tag);
                existing[t] = tag;
            }
            post.Hashtags.Add(new PostHashtag { Hashtag = tag });
        }
    }

    private static string BuildScopeKey(FeedQuery q, Guid? uid)
        => $"{q.Scope.ToString().ToLowerInvariant()}:{uid?.ToString() ?? "anon"}:{q.AnimalType ?? "-"}:{q.Hashtag ?? "-"}";
}
