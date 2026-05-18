using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedReactionService : IFeedReactionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IFeedCache _cache;
    private readonly INotificationService _notify;

    public FeedReactionService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IFeedCache cache, INotificationService notify)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _cache = cache;
        _notify = notify;
    }

    public async Task SetReactionAsync(Guid postId, string type, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!Enum.TryParse<ReactionType>(type, true, out var rt))
            throw new AppException("invalid_reaction", $"Unknown reaction '{type}'.");

        var existing = await _db.Reactions.SingleOrDefaultAsync(r => r.PostId == postId && r.UserId == uid, ct);
        var isNew = existing is null;
        if (existing is null)
            _db.Reactions.Add(new Reaction { PostId = postId, UserId = uid, Type = rt });
        else
            existing.Type = rt;

        await _db.SaveChangesAsync(ct);
        if (isNew) await _cache.BumpReactionCountAsync(postId, +1, ct);
        await _kafka.PublishAsync(FeedTopics.Posts,
            new PostReacted(postId, uid, rt.ToString(), Removed: false, DateTime.UtcNow),
            postId.ToString(), ct);

        // Send notification to post author (only for new reactions, not re-reactions)
        if (isNew)
        {
            var post = await _db.Posts.AsNoTracking()
                .Where(p => p.Id == postId)
                .Select(p => new { p.AuthorId })
                .SingleOrDefaultAsync(ct);
            if (post is not null && post.AuthorId != uid)
            {
                var reactorName = await _db.Users.Where(u => u.Id == uid)
                    .Select(u => u.DisplayName).SingleOrDefaultAsync(ct) ?? "Someone";
                await _notify.NotifyUserAsync(
                    post.AuthorId,
                    "New reaction on your post",
                    $"{reactorName} reacted with {rt} to your post.",
                    new { type = "post_reaction", postId, reactorId = uid, reactionType = rt.ToString() },
                    ct);
            }
        }
    }

    public async Task RemoveReactionAsync(Guid postId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var existing = await _db.Reactions.SingleOrDefaultAsync(r => r.PostId == postId && r.UserId == uid, ct);
        if (existing is null) return;
        _db.Reactions.Remove(existing);
        await _db.SaveChangesAsync(ct);
        await _cache.BumpReactionCountAsync(postId, -1, ct);
        await _kafka.PublishAsync(FeedTopics.Posts,
            new PostReacted(postId, uid, existing.Type.ToString(), Removed: true, DateTime.UtcNow),
            postId.ToString(), ct);
    }
}
