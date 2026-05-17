using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Feed.Events;
using Pawzaroo.Application.Modules.Feed.Services;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Domain.Social;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Feed;

public class FeedReportService : IFeedReportService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;

    public FeedReportService(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
    }

    public async Task ReportPostAsync(Guid postId, string reason, string? details, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!await _db.Posts.AnyAsync(p => p.Id == postId, ct))
            throw new NotFoundException("Post", postId);

        _db.PostReports.Add(new PostReport { PostId = postId, ReporterId = uid, Reason = reason, Details = details });
        _db.ContentReports.Add(new ContentReport
        {
            TargetType = ReportTargetType.Post,
            TargetId = postId,
            ReporterId = uid,
            Reason = reason,
            Details = details
        });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Moderation,
            new PostReported(postId, uid, reason, DateTime.UtcNow), postId.ToString(), ct);
    }

    public async Task ReportCommentAsync(Guid commentId, string reason, string? details, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!await _db.Comments.AnyAsync(c => c.Id == commentId, ct))
            throw new NotFoundException("Comment", commentId);

        _db.ContentReports.Add(new ContentReport
        {
            TargetType = ReportTargetType.Comment,
            TargetId = commentId,
            ReporterId = uid,
            Reason = reason,
            Details = details
        });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(FeedTopics.Moderation,
            new CommentReported(commentId, uid, reason, DateTime.UtcNow), commentId.ToString(), ct);
    }
}
