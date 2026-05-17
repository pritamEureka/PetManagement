namespace Pawzaroo.Application.Modules.Messaging.Dtos;

public record ParticipantDto(Guid UserId, string DisplayName, string? AvatarUrl, bool Online);

public record ConversationSummaryDto(
    Guid Id,
    string? Title,
    bool IsGroup,
    string? ContextType,
    Guid? ContextRefId,
    DateTime? LastMessageAt,
    string? LastMessagePreview,
    int UnreadCount,
    bool IsArchived,
    bool IsMuted,
    IReadOnlyList<ParticipantDto> Participants);

public record AttachmentDto(string Url, string MimeType, long SizeBytes, string? FileName, int? Width, int? Height);

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string? SenderAvatarUrl,
    string Type,
    string? Content,
    string? MediaUrl,
    Guid? ReplyToMessageId,
    bool IsEdited,
    bool IsDeletedForAll,
    DateTime CreatedAt,
    IReadOnlyList<AttachmentDto> Attachments,
    DateTime? DeliveredAt,
    DateTime? ReadAt);

public record StartConversationInput(Guid OtherUserId, string? ContextType, Guid? ContextRefId, string? InitialMessage);

public record SendMessageInput(
    Guid ConversationId,
    string Type,
    string? Content,
    string? MediaUrl,
    Guid? ReplyToMessageId,
    IReadOnlyList<AttachmentInput>? Attachments);

public record AttachmentInput(string Url, string MimeType, long SizeBytes, string? FileName, int? Width, int? Height);

public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

public record ReportMessageInput(string Reason, string? Details);
public record BlockUserInput(Guid BlockedUserId, string? Reason);

public record ReportedMessageDto(
    Guid ReportId,
    Guid MessageId,
    string? Content,
    Guid SenderId,
    string SenderName,
    Guid ReporterId,
    string ReporterName,
    string Reason,
    bool Resolved,
    DateTime ReportedAt);

public record PresenceDto(Guid UserId, bool Online, DateTime? LastSeenAt);
