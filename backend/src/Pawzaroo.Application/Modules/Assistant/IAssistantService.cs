namespace Pawzaroo.Application.Modules.Assistant;

public record SendAssistantMessageRequest(Guid? ConversationId, string Message, Guid? PetId, IReadOnlyList<string>? ImageUrls = null);

public record AssistantMessageDto(Guid Id, string Role, string Content, IReadOnlyList<AssistantCitation> Citations,
    bool Escalated, string? Intent, DateTime CreatedAt, IReadOnlyList<string>? Attachments = null);

public record AssistantConversationSummaryDto(Guid Id, string Title, DateTime LastMessageAt, bool HasEscalation);

public record AssistantConversationDto(Guid Id, string Title, Guid? PetId, DateTime LastMessageAt, bool HasEscalation,
    IReadOnlyList<AssistantMessageDto> Messages);

public record AssistantExchangeDto(Guid ConversationId, AssistantMessageDto UserMessage, AssistantMessageDto AssistantMessage,
    string Disclaimer, string? EscalationLink);

public interface IAssistantService
{
    Task<AssistantExchangeDto> SendAsync(SendAssistantMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AssistantConversationSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<AssistantConversationDto> GetAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
