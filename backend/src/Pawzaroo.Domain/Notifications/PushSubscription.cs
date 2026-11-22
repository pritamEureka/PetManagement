using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Notifications;

/// <summary>A browser Web Push subscription (PushManager.subscribe result) for one user/device.</summary>
public class PushSubscription : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Endpoint { get; set; } = default!;
    public string P256dh { get; set; } = default!;
    public string Auth { get; set; } = default!;
    public string? UserAgent { get; set; }
}

/// <summary>Per-user, per-category channel toggles. A missing row means "all channels enabled".</summary>
public class NotificationPreference : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Category { get; set; } = default!;
    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
}

/// <summary>Per-user quiet hours (push is suppressed inside the window). Minutes are local minutes-of-day.</summary>
public class NotificationQuietHours : AuditableEntity
{
    public Guid UserId { get; set; }
    public bool Enabled { get; set; }
    public int StartMinute { get; set; } = 22 * 60;
    public int EndMinute { get; set; } = 7 * 60;
    /// <summary>Minutes east of UTC (e.g. IST = 330), i.e. -Date.getTimezoneOffset().</summary>
    public int TimezoneOffsetMinutes { get; set; }
}
