namespace Pawzaroo.Application.Modules.Messaging.Events;

public static class MessagingTopics
{
    public const string Messages   = "pawzaroo.message.events";
    public const string Moderation = "pawzaroo.message.moderation";
}

public record MessageSent(Guid MessageId, Guid ConversationId, Guid SenderId,
                          IReadOnlyList<Guid> RecipientIds, DateTime At);
public record MessageDelivered(Guid MessageId, Guid UserId, DateTime At);
public record MessageRead(Guid ConversationId, Guid UserId, Guid? LastMessageId, DateTime At);
public record MessageDeleted(Guid MessageId, Guid ByUserId, bool ByModerator, DateTime At);

public record ConversationStarted(Guid ConversationId, Guid InitiatorId, Guid OtherUserId,
                                  string? ContextType, Guid? ContextRefId, DateTime At);

public record UserBlocked(Guid BlockerId, Guid BlockedUserId, string? Reason, DateTime At);
public record MessageReported(Guid ReportId, Guid MessageId, Guid ReporterId, string Reason, DateTime At);
public record MessageModerated(Guid MessageId, Guid ActorId, string Action, string? Reason, DateTime At);
