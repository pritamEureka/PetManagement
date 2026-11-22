namespace Pawzaroo.Application.Modules.Push;

/// <summary>Known notification categories shown in the settings UI. Producers should use these keys.</summary>
public static class NotificationCategories
{
    public const string Messages = "messages";
    public const string Social = "social";
    public const string Appointments = "appointments";
    public const string Reminders = "reminders";
    public const string Orders = "orders";
    public const string Adoption = "adoption";
    public const string LostFound = "lost_found";
    public const string Community = "community";
    public const string System = "system";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Messages, Social, Appointments, Reminders, Orders, Adoption, LostFound, Community, System,
    };
}

public enum NotificationChannel { InApp, Push, Email }

public record ChannelPrefs(bool InApp, bool Push, bool Email)
{
    public static readonly ChannelPrefs AllOn = new(true, true, true);
}

/// <param name="StartMinute">Local minute-of-day (0-1439) the quiet window starts.</param>
/// <param name="EndMinute">Local minute-of-day the window ends (exclusive). May be less than start (wraps midnight).</param>
/// <param name="TimezoneOffsetMinutes">Minutes east of UTC.</param>
public record QuietHoursSetting(bool Enabled, int StartMinute, int EndMinute, int TimezoneOffsetMinutes);

/// <summary>Pure, side-effect-free delivery rules (unit-testable without a database).</summary>
public static class NotificationPolicy
{
    public static bool IsInQuietHours(QuietHoursSetting? q, DateTime nowUtc)
    {
        if (q is not { Enabled: true }) return false;
        if (q.StartMinute == q.EndMinute) return false; // empty window
        var utc = nowUtc.Kind == DateTimeKind.Local ? nowUtc.ToUniversalTime() : nowUtc;
        var local = utc.AddMinutes(q.TimezoneOffsetMinutes);
        var minute = local.Hour * 60 + local.Minute;
        return q.StartMinute < q.EndMinute
            ? minute >= q.StartMinute && minute < q.EndMinute
            : minute >= q.StartMinute || minute < q.EndMinute; // wraps midnight
    }

    /// <summary>
    /// Channel toggle must be on (missing prefs = on). Quiet hours suppress Push only; in-app and
    /// email are not interruptive.
    /// </summary>
    public static bool ShouldDeliver(NotificationChannel channel, ChannelPrefs? prefs, QuietHoursSetting? quiet, DateTime nowUtc)
    {
        var p = prefs ?? ChannelPrefs.AllOn;
        return channel switch
        {
            NotificationChannel.InApp => p.InApp,
            NotificationChannel.Email => p.Email,
            NotificationChannel.Push => p.Push && !IsInQuietHours(quiet, nowUtc),
            _ => false,
        };
    }
}

public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth, string? UserAgent);

public record PushSubscriptionDto(Guid Id, string Endpoint, string? UserAgent, DateTime CreatedAt);

public record CategoryPreferenceDto(string Category, bool InApp, bool Push, bool Email);

public record QuietHoursDto(bool Enabled, int StartMinute, int EndMinute, int TimezoneOffsetMinutes);

public record NotificationPreferencesDto(IReadOnlyList<CategoryPreferenceDto> Categories, QuietHoursDto QuietHours);

public record VapidKeyDto(string? PublicKey);

public interface IPushSubscriptionService
{
    Task<PushSubscriptionDto> SubscribeAsync(PushSubscribeRequest req, CancellationToken ct = default);
    Task<bool> UnsubscribeAsync(string endpoint, CancellationToken ct = default);
    Task<IReadOnlyList<PushSubscriptionDto>> ListAsync(CancellationToken ct = default);
}

public interface INotificationPreferenceService
{
    Task<NotificationPreferencesDto> GetAsync(CancellationToken ct = default);
    Task<NotificationPreferencesDto> PutAsync(NotificationPreferencesDto dto, CancellationToken ct = default);
}

public record PushMessage(string Title, string Body, string? Url, string? Category);

public enum WebPushResult { Sent, Gone, Failed }

/// <summary>
/// Transport that delivers one payload to one browser subscription. The default registration only
/// logs; swap in a real RFC 8030/8291/8292 (VAPID + aes128gcm) sender without touching callers.
/// Return <see cref="WebPushResult.Gone"/> for HTTP 404/410 so the subscription is pruned.
/// </summary>
public interface IWebPushSender
{
    Task<WebPushResult> SendAsync(string endpoint, string p256dh, string auth, PushMessage message, CancellationToken ct = default);
}

public interface IPushDispatcher
{
    /// <summary>Should a PUSH for <paramref name="category"/> be delivered to the user at <paramref name="now"/>?</summary>
    Task<bool> ShouldDeliverAsync(Guid userId, string category, DateTime now, CancellationToken ct = default);

    /// <summary>Checks preferences/quiet hours, then pushes to all of the user's subscriptions. Returns the number sent.</summary>
    Task<int> DispatchAsync(Guid userId, PushMessage message, CancellationToken ct = default);
}
