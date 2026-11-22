using System.Text.Json;

namespace Pawzaroo.Application.Modules.Push;

public record NotificationMuteDto(Guid Id, string Scope, string ScopeId, DateTime? MutedUntil, DateTime CreatedAt);

public record CreateNotificationMuteRequest(string Scope, string ScopeId, int? DurationHours);

public record DigestPreferenceDto(string Category, string Frequency);

public interface INotificationMuteService
{
    Task<IReadOnlyList<NotificationMuteDto>> ListAsync(CancellationToken ct = default);
    Task<NotificationMuteDto> MuteAsync(CreateNotificationMuteRequest req, CancellationToken ct = default);
    Task<bool> UnmuteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DigestPreferenceDto>> GetDigestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DigestPreferenceDto>> PutDigestAsync(IReadOnlyList<DigestPreferenceDto> prefs, CancellationToken ct = default);

    /// <summary>Is the (category, source keys) combination currently muted for the user?
    /// Keys look like "conversation:{id}", "community:{id}", "source:{id}".</summary>
    Task<bool> IsMutedAsync(Guid userId, string? category, IEnumerable<string>? sourceKeys, DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>Pure mute rules + category inference for legacy in-app rows that carry no category column.</summary>
public static class NotificationMutePolicy
{
    public static bool IsActive(DateTime? mutedUntil, DateTime nowUtc) => mutedUntil is null || mutedUntil > nowUtc;

    /// <summary>Infers a category from the notification payload "type" / url. Null when unknown.</summary>
    public static string? InferCategory(string? payload, string? url)
    {
        var type = PayloadString(payload, "type");
        if (type is "post_reaction" or "post_comment") return NotificationCategories.Social;
        if (type is { Length: > 0 } && type.StartsWith("message", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Messages;
        if (url is null) return null;
        if (url.StartsWith("/messages", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Messages;
        if (url.StartsWith("/appointments", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Appointments;
        if (url.StartsWith("/orders", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Orders;
        if (url.StartsWith("/adoption", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Adoption;
        if (url.StartsWith("/lost-found", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.LostFound;
        if (url.StartsWith("/communities", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Community;
        if (url.StartsWith("/feed", StringComparison.OrdinalIgnoreCase)) return NotificationCategories.Social;
        return null;
    }

    /// <summary>Source keys derivable from a payload (actor ids, conversation, community).</summary>
    public static IReadOnlyList<string> SourceKeys(string? payload)
    {
        var keys = new List<string>();
        foreach (var (field, prefix) in new[]
        {
            ("reactorId", "source"), ("commenterId", "source"), ("senderId", "source"), ("actorId", "source"),
            ("conversationId", "conversation"), ("communityId", "community"),
        })
        {
            var v = PayloadString(payload, field);
            if (!string.IsNullOrEmpty(v)) keys.Add($"{prefix}:{v}".ToLowerInvariant());
        }
        return keys;
    }

    private static string? PayloadString(string? payload, string field)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(field, out var el)
                   && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }
}
