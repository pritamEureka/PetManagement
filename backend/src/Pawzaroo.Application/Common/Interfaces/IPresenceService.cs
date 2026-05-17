namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>
/// Redis-backed online presence. A user is "online" if at least one SignalR
/// connection is tracked for them. Hub OnConnected/OnDisconnected drive the
/// counter; queries return a bool that's fast (~O(1) GET) and cheap.
/// </summary>
public interface IPresenceService
{
    Task TrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default);
    Task UntrackConnectionAsync(Guid userId, string connectionId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, bool>> AreOnlineAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task<DateTime?> GetLastSeenAsync(Guid userId, CancellationToken ct = default);
    Task TouchLastSeenAsync(Guid userId, CancellationToken ct = default);
}
