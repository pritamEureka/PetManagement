using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Assistant;
using Pawzaroo.Domain.Assistant;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Assistant;

public class AssistantService : IAssistantService
{
    private const int MaxMessageLength = 2000;
    private const int RateLimitPerMinute = 12;
    private const int HistoryTurns = 6;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAssistantEngine _engine;
    private readonly IAuditLogger _audit;
    private readonly IRedisRateLimiter _limiter;

    public AssistantService(ApplicationDbContext db, ICurrentUserService current, IAssistantEngine engine,
        IAuditLogger audit, IRedisRateLimiter limiter)
    {
        _db = db;
        _current = current;
        _engine = engine;
        _audit = audit;
        _limiter = limiter;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<AssistantExchangeDto> SendAsync(SendAssistantMessageRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var text = (req.Message ?? "").Trim();
        if (text.Length == 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["message"] = new[] { "Message is required." } });
        if (text.Length > MaxMessageLength)
            throw new ValidationException(new Dictionary<string, string[]> { ["message"] = new[] { $"Message must be at most {MaxMessageLength} characters." } });

        var decision = await _limiter.CheckAsync("assistant", uid.ToString(), RateLimitPerMinute, TimeSpan.FromMinutes(1), ct);
        if (!decision.Allowed)
            throw new AppException("rate_limited",
                $"You are sending messages too quickly. Please wait {Math.Max(1, (int)decision.ResetIn.TotalSeconds)} seconds.");

        AssistantConversation conv;
        if (req.ConversationId is { } cid)
            conv = await _db.Set<AssistantConversation>().SingleOrDefaultAsync(c => c.Id == cid && c.UserId == uid, ct)
                   ?? throw new NotFoundException("Conversation", cid);
        else
        {
            conv = new AssistantConversation
            {
                UserId = uid,
                PetId = req.PetId,
                Title = text.Length <= 60 ? text : text[..57] + "..."
            };
            _db.Set<AssistantConversation>().Add(conv);
        }

        var history = req.ConversationId is null
            ? new List<AssistantTurn>()
            : (await _db.Set<AssistantMessage>().AsNoTracking()
                    .Where(m => m.ConversationId == conv.Id)
                    .OrderByDescending(m => m.CreatedAt).Take(HistoryTurns)
                    .Select(m => new { m.Role, m.Content, m.CreatedAt }).ToListAsync(ct))
                .OrderBy(m => m.CreatedAt).Select(m => new AssistantTurn(m.Role, m.Content)).ToList();

        var reply = await _engine.RespondAsync(new AssistantRequest(uid, text, req.PetId ?? conv.PetId, history), ct);

        var now = DateTime.UtcNow;
        var images = NormalizeImages(req.ImageUrls);
        var botContent = reply.Content;
        if (images.Count > 0)
        {
            // No vision model: photos are stored for a human vet and never change the (emergency) engine reply.
            botContent += "\n\n" + SymptomChecklistScorer.PhotoNotice + " A review request has been sent to our vets.";
            _db.Set<AssistantReviewRequest>().Add(new AssistantReviewRequest
            {
                UserId = uid, ConversationId = conv.Id, PetId = req.PetId ?? conv.PetId,
                Urgency = reply.Escalated ? TriageUrgency.Emergency : TriageUrgency.Soon,
                Summary = "Photo attached to assistant question: " + Truncate(text, 500),
                ImageUrlsJson = JsonSerializer.Serialize(images, Json),
            });
        }
        var userMsg = new AssistantMessage
        {
            ConversationId = conv.Id, Conversation = conv, Role = "user", Content = text, CreatedAt = now,
            AttachmentsJson = images.Count > 0 ? JsonSerializer.Serialize(images, Json) : null
        };
        var botMsg = new AssistantMessage
        {
            ConversationId = conv.Id, Conversation = conv, Role = "assistant", Content = botContent,
            CitationsJson = JsonSerializer.Serialize(reply.Citations, Json), Escalated = reply.Escalated,
            Intent = reply.Intent, CreatedAt = now.AddMilliseconds(1)
        };
        _db.Set<AssistantMessage>().AddRange(userMsg, botMsg);
        conv.LastMessageAt = botMsg.CreatedAt;
        if (reply.Escalated) conv.HasEscalation = true;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("assistant.exchange", nameof(AssistantConversation), conv.Id.ToString(), "assistant",
            null, new
            {
                intent = reply.Intent,
                escalated = reply.Escalated,
                petId = req.PetId ?? conv.PetId,
                question = Truncate(text, 500),
                answer = Truncate(reply.Content, 500),
                citations = reply.Citations.Select(c => $"{c.SourceType}:{c.Id}").ToArray()
            }, ct);

        return new AssistantExchangeDto(conv.Id, ToDto(userMsg), ToDto(botMsg), reply.Disclaimer, reply.EscalationLink);
    }

    public async Task<IReadOnlyList<AssistantConversationSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.Set<AssistantConversation>().AsNoTracking().Where(c => c.UserId == uid)
            .OrderByDescending(c => c.LastMessageAt).Take(100)
            .Select(c => new AssistantConversationSummaryDto(c.Id, c.Title, c.LastMessageAt, c.HasEscalation))
            .ToListAsync(ct);
    }

    public async Task<AssistantConversationDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = await _db.Set<AssistantConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("Conversation", id);
        var msgs = await _db.Set<AssistantMessage>().AsNoTracking().Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt).ToListAsync(ct);
        return new AssistantConversationDto(c.Id, c.Title, c.PetId, c.LastMessageAt, c.HasEscalation, msgs.Select(ToDto).ToList());
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var c = await _db.Set<AssistantConversation>().SingleOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct)
                ?? throw new NotFoundException("Conversation", id);
        // Conversation is soft-deleted by the DbContext; transcripts are removed outright.
        var msgs = await _db.Set<AssistantMessage>().Where(m => m.ConversationId == id).ToListAsync(ct);
        _db.Set<AssistantMessage>().RemoveRange(msgs);
        _db.Set<AssistantConversation>().Remove(c);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("assistant.conversation.delete", nameof(AssistantConversation), id.ToString(), "assistant", ct: ct);
    }

    private static AssistantMessageDto ToDto(AssistantMessage m)
    {
        IReadOnlyList<AssistantCitation> cites = string.IsNullOrEmpty(m.CitationsJson)
            ? Array.Empty<AssistantCitation>()
            : JsonSerializer.Deserialize<List<AssistantCitation>>(m.CitationsJson, Json) ?? new List<AssistantCitation>();
        IReadOnlyList<string>? attachments = string.IsNullOrEmpty(m.AttachmentsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(m.AttachmentsJson, Json);
        return new AssistantMessageDto(m.Id, m.Role, m.Content, cites, m.Escalated, m.Intent, m.CreatedAt, attachments);
    }

    /// <summary>Up to 4 distinct https image URLs (each at most 2048 chars); anything else is a validation error.</summary>
    internal static List<string> NormalizeImages(IReadOnlyList<string>? urls)
    {
        var list = (urls ?? Array.Empty<string>()).Select(u => (u ?? "").Trim()).Where(u => u.Length > 0).Distinct().ToList();
        if (list.Count > 4 || list.Any(u => u.Length > 2048 || !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ValidationException(new Dictionary<string, string[]> { ["imageUrls"] = new[] { "Attach up to 4 images (https URLs)." } });
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
