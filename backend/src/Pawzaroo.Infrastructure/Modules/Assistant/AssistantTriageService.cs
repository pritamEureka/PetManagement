using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Assistant;
using Pawzaroo.Domain.Assistant;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Assistant;

/// <summary>
/// Checklist triage + human vet review queue. Scoring is deterministic (<see cref="SymptomChecklistScorer"/>);
/// photos are only stored for a human vet and never interpreted.
/// </summary>
public class AssistantTriageService : IAssistantTriageService
{
    public const string BookingLink = "/vets?online=true";
    private const string Disclaimer = "General information only - not veterinary advice. In an emergency contact a vet immediately.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AssistantTriageService> _log;

    public AssistantTriageService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify,
        IAuditLogger audit, ILogger<AssistantTriageService> log)
    {
        _db = db;
        _current = current;
        _notify = notify;
        _audit = audit;
        _log = log;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<TriageResultDto> SubmitAsync(SubmitTriageRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = req.Checklist ?? throw Invalid("checklist", "Checklist is required.");
        if (string.IsNullOrWhiteSpace(c.Species) || c.Species.Length > 40) throw Invalid("species", "Species is required.");
        if (c.Notes is { Length: > 1000 }) throw Invalid("notes", "Notes must be at most 1000 characters.");
        if (c.VomitingCount is < 0 or > 100 || c.DiarrhoeaCount is < 0 or > 100) throw Invalid("checklist", "Counts must be between 0 and 100.");
        var images = AssistantService.NormalizeImages(req.ImageUrls);

        var assessment = SymptomChecklistScorer.Score(c, images.Count > 0);

        AssistantConversation conv;
        if (req.ConversationId is { } cid)
            conv = await _db.Set<AssistantConversation>().SingleOrDefaultAsync(x => x.Id == cid && x.UserId == uid, ct)
                   ?? throw new NotFoundException("Conversation", cid);
        else
        {
            conv = new AssistantConversation { UserId = uid, PetId = req.PetId, Title = $"Symptom check: {c.Species.Trim()}" };
            _db.Set<AssistantConversation>().Add(conv);
        }

        var emergency = assessment.Urgency == TriageUrgency.Emergency;
        var now = DateTime.UtcNow;
        var userMsg = new AssistantMessage
        {
            ConversationId = conv.Id, Conversation = conv, Role = "user", CreatedAt = now,
            Content = Truncate(DescribeChecklist(c), 4000),
            AttachmentsJson = images.Count > 0 ? JsonSerializer.Serialize(images, Json) : null,
        };
        var reply = BuildReply(assessment, images.Count > 0);
        var botMsg = new AssistantMessage
        {
            ConversationId = conv.Id, Conversation = conv, Role = "assistant", Content = reply, Escalated = emergency,
            Intent = "triage", CreatedAt = now.AddMilliseconds(1),
            CitationsJson = JsonSerializer.Serialize(new[]
            {
                new AssistantCitation("knowledge", $"triage:{assessment.Urgency.ToLowerInvariant()}",
                    $"Symptom checklist: {assessment.Urgency}", emergency ? RuleBasedAssistantEngine.EscalationLink : BookingLink),
            }, Json),
        };
        _db.Set<AssistantMessage>().AddRange(userMsg, botMsg);
        conv.LastMessageAt = botMsg.CreatedAt;
        if (emergency) conv.HasEscalation = true;

        var review = new AssistantReviewRequest
        {
            UserId = uid, ConversationId = conv.Id, PetId = req.PetId ?? conv.PetId, Urgency = assessment.Urgency,
            Summary = Truncate($"{c.Species.Trim()}: {assessment.Summary} Reasons: {string.Join("; ", assessment.Reasons)}", 2000),
            ChecklistJson = JsonSerializer.Serialize(c, Json),
            ImageUrlsJson = images.Count > 0 ? JsonSerializer.Serialize(images, Json) : null,
        };
        _db.Set<AssistantReviewRequest>().Add(review);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("assistant.triage", nameof(AssistantReviewRequest), review.Id.ToString(), "assistant", null,
            new { urgency = assessment.Urgency, photos = images.Count, petId = review.PetId }, ct);

        return new TriageResultDto(conv.Id, ToMessageDto(botMsg), assessment, BookingLink,
            emergency || assessment.Urgency == TriageUrgency.Urgent ? RuleBasedAssistantEngine.EscalationLink : null,
            review.Id, Disclaimer);
    }

    public async Task<IReadOnlyList<ReviewRequestDto>> ListReviewsAsync(string? status, int take = 50, CancellationToken ct = default)
    {
        Uid();
        take = Math.Clamp(take, 1, 200);
        var q = _db.Set<AssistantReviewRequest>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AssistantReviewStatus>(status, true, out var s)) throw Invalid("status", "Status must be Pending or Reviewed.");
            q = q.Where(x => x.Status == s);
        }
        // Most urgent first within the page of newest requests.
        var rows = await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        return rows.OrderByDescending(x => UrgencyRank(x.Urgency)).ThenBy(x => x.CreatedAt).Select(ToDto).ToList();
    }

    public async Task<ReviewRequestDto> AnswerReviewAsync(Guid id, AnswerReviewRequest req, CancellationToken ct = default)
    {
        var vetId = Uid();
        var notes = (req?.VetNotes ?? "").Trim();
        if (notes.Length is 0 or > 4000) throw Invalid("vetNotes", "Notes are required (max 4000 characters).");

        var r = await _db.Set<AssistantReviewRequest>().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("ReviewRequest", id);
        r.Status = AssistantReviewStatus.Reviewed;
        r.VetNotes = notes;
        r.ReviewedByUserId = vetId;
        r.ReviewedAt = DateTime.UtcNow;

        // Surface the answer in the owner's conversation so it appears where they asked.
        var conv = await _db.Set<AssistantConversation>().FirstOrDefaultAsync(x => x.Id == r.ConversationId, ct);
        if (conv is not null)
        {
            var msg = new AssistantMessage
            {
                ConversationId = conv.Id, Conversation = conv, Role = "assistant", Intent = "vet_review",
                Content = "A veterinarian reviewed your request:\n\n" + notes, CreatedAt = DateTime.UtcNow,
            };
            _db.Set<AssistantMessage>().Add(msg);
            conv.LastMessageAt = msg.CreatedAt;
        }
        await _db.SaveChangesAsync(ct);

        try
        {
            await _notify.NotifyUserAsync(r.UserId, "A vet reviewed your assistant request",
                "Open the care assistant to read the vet's notes.",
                new { type = "assistant_review", url = "/assistant", conversationId = r.ConversationId }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Could not notify user about vet review {ReviewId}", r.Id);
        }
        await _audit.LogAsync("assistant.review.answer", nameof(AssistantReviewRequest), r.Id.ToString(), "assistant", ct: ct);
        return ToDto(r);
    }

    public async Task<IReadOnlyList<ReviewRequestDto>> ListMyReviewsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var rows = await _db.Set<AssistantReviewRequest>().AsNoTracking().Where(x => x.UserId == uid)
            .OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    // ---- helpers ---------------------------------------------------------------------------

    internal static string BuildReply(TriageAssessment a, bool hasPhotos)
    {
        var lines = new List<string> { $"Urgency: {a.Urgency}", a.Summary, "", "Why:" };
        lines.AddRange(a.Reasons.Select(r => "- " + r));
        lines.Add("");
        if (a.Urgency is TriageUrgency.Emergency)
            lines.Add($"Open the vet directory ({RuleBasedAssistantEngine.EscalationLink}) and call a vet now.");
        lines.Add($"You can book an online vet appointment: {BookingLink}");
        lines.Add(hasPhotos
            ? SymptomChecklistScorer.PhotoNotice + " A review request has been recorded for our vets."
            : "A review request has been recorded so a vet can look at this checklist.");
        return string.Join("\n", lines);
    }

    internal static string DescribeChecklist(SymptomChecklist c) =>
        $"Symptom checklist - species: {c.Species}; duration: {c.Duration}; appetite: {c.Appetite}; breathing: {c.Breathing}; " +
        $"gums: {c.GumColour}; bleeding: {c.Bleeding}; vomiting (24h): {c.VomitingCount}; diarrhoea (24h): {c.DiarrhoeaCount}; " +
        $"lethargy: {c.Lethargy}; seizure: {(c.Seizure ? "yes" : "no")}; toxin exposure: {(c.PossibleToxinExposure ? "yes" : "no")}; " +
        $"very young/senior: {(c.YoungOrSenior ? "yes" : "no")}" + (string.IsNullOrWhiteSpace(c.Notes) ? "" : $"; notes: {c.Notes}");

    private static int UrgencyRank(string u) => u switch
    {
        TriageUrgency.Emergency => 3, TriageUrgency.Urgent => 2, TriageUrgency.Soon => 1, _ => 0,
    };

    private static ReviewRequestDto ToDto(AssistantReviewRequest r)
    {
        var imgs = string.IsNullOrEmpty(r.ImageUrlsJson) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(r.ImageUrlsJson, Json) ?? new();
        var chk = string.IsNullOrEmpty(r.ChecklistJson) ? null : JsonSerializer.Deserialize<SymptomChecklist>(r.ChecklistJson, Json);
        return new ReviewRequestDto(r.Id, r.ConversationId, r.PetId, r.UserId, r.Urgency, r.Summary, imgs, chk,
            r.Status.ToString(), r.VetNotes, r.CreatedAt, r.ReviewedAt);
    }

    private static AssistantMessageDto ToMessageDto(AssistantMessage m)
    {
        var cites = string.IsNullOrEmpty(m.CitationsJson)
            ? new List<AssistantCitation>()
            : JsonSerializer.Deserialize<List<AssistantCitation>>(m.CitationsJson, Json) ?? new();
        return new AssistantMessageDto(m.Id, m.Role, m.Content, cites, m.Escalated, m.Intent, m.CreatedAt, null);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static Exception Invalid(string field, string msg) =>
        new ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });
}
