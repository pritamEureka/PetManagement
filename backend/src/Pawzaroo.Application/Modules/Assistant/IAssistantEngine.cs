namespace Pawzaroo.Application.Modules.Assistant;

public record AssistantTurn(string Role, string Content);

public record AssistantCitation(string SourceType, string Id, string Title, string? Url);

public record AssistantRequest(Guid UserId, string Message, Guid? PetId, IReadOnlyList<AssistantTurn> History);

public record AssistantReply(string Content, IReadOnlyList<AssistantCitation> Citations, bool Escalated,
    string Intent, string Disclaimer, string? EscalationLink);

/// <summary>
/// Produces a grounded reply for one user turn.
///
/// How an LLM-backed engine plugs in: implement this interface (e.g. <c>LlmAssistantEngine</c>) and swap the
/// registration in <c>AddIntelligenceFeatures</c>. A safe implementation should (1) ALWAYS run the deterministic
/// <c>TriageDetector</c> first and short-circuit to the escalation reply on a hit, never letting the model
/// answer emergencies; (2) retrieve context (pet records, <c>PetKnowledge</c>, catalog, vets) exactly as the
/// rule-based engine does and pass it in the prompt as numbered sources; (3) require the model to cite sources
/// by number and map them back to <see cref="AssistantCitation"/>, dropping uncited claims; (4) append the
/// standard disclaimer; and (5) fall back to <c>RuleBasedAssistantEngine</c> on provider error or timeout.
/// The conversation store, rate limiting and audit logging live in the service layer and need no change.
/// </summary>
public interface IAssistantEngine
{
    Task<AssistantReply> RespondAsync(AssistantRequest request, CancellationToken ct = default);
}
