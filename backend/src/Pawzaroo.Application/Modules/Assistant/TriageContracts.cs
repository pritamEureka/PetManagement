namespace Pawzaroo.Application.Modules.Assistant;

/// <summary>
/// Structured symptom checklist. String fields are case-insensitive keywords:
/// duration: lt24h | 1to3d | gt3d; appetite: normal | reduced | none; breathing: normal | fast | laboured;
/// gumColour: pink | pale | blue | yellow | unknown; bleeding: none | minor | heavy; lethargy: none | mild | severe.
/// Unknown values are treated as the mildest option (the scorer never invents severity).
/// </summary>
public record SymptomChecklist(
    string Species,
    string Duration,
    string Appetite,
    string Breathing,
    string GumColour,
    string Bleeding,
    int VomitingCount,
    int DiarrhoeaCount,
    string Lethargy,
    bool Seizure,
    bool PossibleToxinExposure,
    bool YoungOrSenior,
    string? Notes);

public static class TriageUrgency
{
    public const string Routine = "Routine";
    public const string Soon = "Soon";
    public const string Urgent = "Urgent";
    public const string Emergency = "Emergency";
}

/// <summary>Deterministic outcome. <see cref="NeedsHumanReview"/> is always true: no model inspects photos.</summary>
public record TriageAssessment(string Urgency, IReadOnlyList<string> Reasons, bool NeedsHumanReview, string Summary);

public record SubmitTriageRequest(Guid? ConversationId, Guid? PetId, SymptomChecklist Checklist, IReadOnlyList<string>? ImageUrls);

public record TriageResultDto(Guid ConversationId, AssistantMessageDto AssistantMessage, TriageAssessment Assessment,
    string BookingLink, string? EscalationLink, Guid ReviewRequestId, string Disclaimer);

public record ReviewRequestDto(Guid Id, Guid ConversationId, Guid? PetId, Guid UserId, string Urgency, string Summary,
    IReadOnlyList<string> ImageUrls, SymptomChecklist? Checklist, string Status, string? VetNotes, DateTime CreatedAt,
    DateTime? ReviewedAt);

public record AnswerReviewRequest(string VetNotes);

public interface IAssistantTriageService
{
    Task<TriageResultDto> SubmitAsync(SubmitTriageRequest request, CancellationToken ct = default);

    /// <summary>Vet queue. status: Pending | Reviewed | null (all).</summary>
    Task<IReadOnlyList<ReviewRequestDto>> ListReviewsAsync(string? status, int take = 50, CancellationToken ct = default);

    Task<ReviewRequestDto> AnswerReviewAsync(Guid id, AnswerReviewRequest request, CancellationToken ct = default);

    /// <summary>The signed-in user's own review requests (so they can see vet answers).</summary>
    Task<IReadOnlyList<ReviewRequestDto>> ListMyReviewsAsync(CancellationToken ct = default);
}
