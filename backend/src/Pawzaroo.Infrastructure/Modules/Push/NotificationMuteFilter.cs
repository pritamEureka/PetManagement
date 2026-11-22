using Pawzaroo.Application.Modules.Push;
using Pawzaroo.Domain.Notifications;

namespace Pawzaroo.Infrastructure.Modules.Push;

/// <summary>
/// Pure helper applying a user's ACTIVE mutes to in-app rows (which carry no category column, so the
/// category is inferred from payload/url). Shared by the notification list and the unread-count endpoint
/// so the bell badge always matches what the list shows.
/// </summary>
public static class NotificationMuteFilter
{
    public static bool IsMuted(IReadOnlyCollection<(NotificationMuteScope Scope, string ScopeId)> mutes, string? payload, string? url)
    {
        if (mutes.Count == 0) return false;
        var category = NotificationMutePolicy.InferCategory(payload, url);
        var keys = NotificationMutePolicy.SourceKeys(payload);
        foreach (var (scope, scopeId) in mutes)
        {
            if (scope == NotificationMuteScope.Category)
            {
                if (category is not null && category == scopeId) return true;
            }
            else if (keys.Contains($"{scope.ToString().ToLowerInvariant()}:{scopeId}")) return true;
        }
        return false;
    }
}
