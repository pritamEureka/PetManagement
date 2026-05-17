using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedShareService : IFeedShareService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public FeedShareService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task ShareAsync(Guid postId, string? note, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        _db.PostShares.Add(new PostShare { PostId = postId, UserId = uid, Note = note });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Posts, new PostShared(postId, uid, DateTime.UtcNow), postId.ToString(), ct);
    }
}
