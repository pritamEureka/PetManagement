using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Notifications;

public enum NotificationMuteScope
{
    /// <summary>Whole notification category (ScopeId = category key, e.g. "social").</summary>
    Category = 0,
    /// <summary>One messaging conversation (ScopeId = conversation id).</summary>
    Conversation = 1,
    /// <summary>One community (ScopeId = community id).</summary>
    Community = 2,
    /// <summary>One actor/source such as another user (ScopeId = user id).</summary>
    Source = 3,
}

/// <summary>A user's mute of a notification scope. MutedUntil null = until unmuted.</summary>
public class NotificationMute : AuditableEntity
{
    public Guid UserId { get; set; }
    public NotificationMuteScope Scope { get; set; }
    public string ScopeId { get; set; } = default!;
    public DateTime? MutedUntil { get; set; }
}

public enum NotificationDigestFrequency
{
    Instant = 0,
    Hourly = 1,
    Daily = 2,
}

/// <summary>
/// Per-user, per-category delivery frequency. Side table next to NotificationPreference (which is left
/// untouched). A missing row means Instant.
/// </summary>
public class NotificationDigestPreference : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Category { get; set; } = default!;
    public NotificationDigestFrequency Frequency { get; set; } = NotificationDigestFrequency.Instant;
    public DateTime? LastDigestAt { get; set; }
}
