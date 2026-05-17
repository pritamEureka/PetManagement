using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Messaging.Dtos;
using Pawzaroo.Application.Modules.Messaging.Events;
using Pawzaroo.Application.Modules.Messaging.Services;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Moderation;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

public class MessageModerationService : IMessageModerationService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public MessageModerationService(ApplicationDbContext db, ICurrentUserService current,
        IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
    }

    public async Task BlockAsync(BlockUserInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (uid == input.BlockedUserId) throw new ConflictException("You can't block yourself.");
        if (await _db.UserBlocks.AnyAsync(b => b.BlockerId == uid && b.BlockedUserId == input.BlockedUserId, ct)) return;

        _db.UserBlocks.Add(new UserBlock { BlockerId = uid, BlockedUserId = input.BlockedUserId, Reason = input.Reason });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MessagingTopics.Moderation,
            new UserBlocked(uid, input.BlockedUserId, input.Reason, DateTime.UtcNow),
            $"{uid}:{input.BlockedUserId}", ct);
    }

    public async Task UnblockAsync(Guid blockedUserId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var b = await _db.UserBlocks.SingleOrDefaultAsync(x => x.BlockerId == uid && x.BlockedUserId == blockedUserId, ct);
        if (b is null) return;
        _db.UserBlocks.Remove(b);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListBlockedAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerId == uid).Select(b => b.BlockedUserId).ToListAsync(ct);
    }

    public async Task ReportMessageAsync(Guid messageId, ReportMessageInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!await _db.Messages.AnyAsync(m => m.Id == messageId, ct))
            throw new NotFoundException("Message", messageId);

        var report = new MessageReport { MessageId = messageId, ReporterId = uid, Reason = input.Reason };
        _db.MessageReports.Add(report);
        _db.ContentReports.Add(new ContentReport
        {
            TargetType = ReportTargetType.Message,
            TargetId = messageId,
            ReporterId = uid,
            Reason = input.Reason,
            Details = input.Details
        });
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(MessagingTopics.Moderation,
            new MessageReported(report.Id, messageId, uid, input.Reason, DateTime.UtcNow),
            messageId.ToString(), ct);
    }

    public async Task<IReadOnlyList<ReportedMessageDto>> ListReportedAsync(bool resolved, CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(Application.Common.Permissions.Permissions.Messaging.Moderate))
            throw new ForbiddenException();

        return await _db.MessageReports.AsNoTracking()
            .Where(r => r.Resolved == resolved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReportedMessageDto(
                r.Id, r.MessageId, r.Message.Content, r.Message.SenderId, r.Message.Sender.DisplayName,
                r.ReporterId, r.Reporter.DisplayName, r.Reason, r.Resolved, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task ResolveReportAsync(Guid reportId, bool deleteMessage, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!_current.Permissions.Contains(Application.Common.Permissions.Permissions.Messaging.Moderate))
            throw new ForbiddenException();

        var report = await _db.MessageReports.Include(r => r.Message)
            .SingleOrDefaultAsync(r => r.Id == reportId, ct)
            ?? throw new NotFoundException("MessageReport", reportId);
        report.Resolved = true;

        if (deleteMessage)
        {
            report.Message.IsDeletedForAll = true;
            report.Message.Content = null;
            report.Message.MediaUrl = null;
            await _kafka.PublishAsync(MessagingTopics.Moderation,
                new MessageModerated(report.MessageId, uid, "deleted", report.Reason, DateTime.UtcNow),
                report.MessageId.ToString(), ct);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("resolve_report", "MessageReport", report.Id.ToString(), "Messaging",
            newValues: new { deleteMessage }, ct: ct);
    }
}
