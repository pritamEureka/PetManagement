using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed.Dtos;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedSavedPostService : IFeedSavedPostService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IFeedService _feed;

    public FeedSavedPostService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IFeedService feed)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _feed = feed;
    }

    public async Task<bool> ToggleAsync(Guid postId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var existing = await _db.PostSaves.SingleOrDefaultAsync(s => s.PostId == postId && s.UserId == uid, ct);
        bool saved;
        if (existing is null)
        {
            if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct))
                throw new NotFoundException("Post", postId);
            _db.PostSaves.Add(new PostSave { PostId = postId, UserId = uid });
            saved = true;
        }
        else
        {
            _db.PostSaves.Remove(existing);
            saved = false;
        }
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Posts,
            new PostSavedEvt(postId, uid, saved, DateTime.UtcNow),
            postId.ToString(), ct);
        return saved;
    }

    public Task<CursorPage<FeedItemDto>> ListAsync(string? cursor, int pageSize, CancellationToken ct = default)
        => _feed.GetFeedAsync(new FeedQuery(Scope: FeedScope.Saved, Cursor: cursor, PageSize: pageSize), ct);
}
