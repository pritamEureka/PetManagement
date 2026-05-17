using Pawzaroo.Application.Modules.Messaging.Dtos;

namespace Pawzaroo.Application.Modules.Messaging.Services;

public interface IMessagingService
{
    Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(bool includeArchived, string? search, CancellationToken ct = default);
    Task<ConversationSummaryDto?> GetConversationAsync(Guid id, CancellationToken ct = default);
    Task<Guid> StartConversationAsync(StartConversationInput input, CancellationToken ct = default);

    Task<CursorPage<MessageDto>> GetMessagesAsync(Guid conversationId, string? cursor, int pageSize, CancellationToken ct = default);

    Task<MessageDto> SendMessageAsync(SendMessageInput input, CancellationToken ct = default);
    Task DeleteMessageAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Marks every unread message from others as read, up to + including lastMessageId (or all if null).</summary>
    Task<Guid?> MarkReadAsync(Guid conversationId, Guid? lastMessageId, CancellationToken ct = default);

    /// <summary>Records that a message reached a user's connected client.</summary>
    Task AckDeliveredAsync(Guid messageId, CancellationToken ct = default);

    Task SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default);
    Task SetMutedAsync(Guid conversationId, bool muted, CancellationToken ct = default);

    Task<int> GetTotalUnreadAsync(CancellationToken ct = default);
}

public interface IMessageModerationService
{
    Task BlockAsync(BlockUserInput input, CancellationToken ct = default);
    Task UnblockAsync(Guid blockedUserId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListBlockedAsync(CancellationToken ct = default);

    Task ReportMessageAsync(Guid messageId, ReportMessageInput input, CancellationToken ct = default);

    // Admin
    Task<IReadOnlyList<ReportedMessageDto>> ListReportedAsync(bool resolved, CancellationToken ct = default);
    Task ResolveReportAsync(Guid reportId, bool deleteMessage, CancellationToken ct = default);
}
