namespace Pawzaroo.Infrastructure.Caching;

/// <summary>Minimal cache + distributed-lock surface needed by <see cref="SingleFlight"/> (Redis in prod, fake in tests).</summary>
public interface ISingleFlightBackend
{
    Task<(bool Hit, T? Value)> TryGetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct);

    /// <summary>Returns a lock token, or null when another node holds the lock. Throws if the backend is down.</summary>
    Task<string?> TryLockAsync(string key, TimeSpan ttl, CancellationToken ct);
    Task UnlockAsync(string key, string token, CancellationToken ct);
}

/// <summary>
/// Cache-stampede protection. Per key: callers in this process queue on a SemaphoreSlim (one loader runs,
/// the rest re-read the cache); across nodes a short backend lock lets one node load while peers briefly poll
/// the cache. Everything fails open: if the backend is unavailable or the peer is slow, the caller loads itself.
/// </summary>
public sealed class SingleFlight
{
    private sealed class Gate { public readonly SemaphoreSlim Sem = new(1, 1); public int Refs; }

    private static readonly Dictionary<string, Gate> Gates = new();

    private readonly ISingleFlightBackend _backend;
    private readonly TimeSpan _lockTtl;
    private readonly TimeSpan _peerWait;
    private readonly TimeSpan _poll;

    public SingleFlight(ISingleFlightBackend backend, TimeSpan? lockTtl = null, TimeSpan? peerWait = null, TimeSpan? poll = null)
    {
        _backend = backend;
        _lockTtl = lockTtl ?? TimeSpan.FromSeconds(10);
        _peerWait = peerWait ?? TimeSpan.FromSeconds(2);
        _poll = poll ?? TimeSpan.FromMilliseconds(50);
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> loader, TimeSpan ttl, CancellationToken ct = default)
    {
        var first = await SafeGetAsync<T>(key, ct);
        if (first.Hit) return first.Value;

        var gate = Acquire(key);
        try { await gate.Sem.WaitAsync(ct); }
        catch { Release(key, gate); throw; }
        try
        {
            var again = await SafeGetAsync<T>(key, ct);
            if (again.Hit) return again.Value;

            string? token = null;
            try { token = await _backend.TryLockAsync(key, _lockTtl, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* backend down: fail open */ }

            if (token is null)
            {
                // Another node is (probably) loading: give it a moment to populate the cache.
                var deadline = DateTime.UtcNow + _peerWait;
                while (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(_poll, ct);
                    var peer = await SafeGetAsync<T>(key, ct);
                    if (peer.Hit) return peer.Value;
                }
            }

            try
            {
                var value = await loader(ct);
                if (value is not null)
                {
                    try { await _backend.SetAsync(key, value, ttl, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* cache is best-effort */ }
                }
                return value;
            }
            finally
            {
                if (token is not null)
                {
                    try { await _backend.UnlockAsync(key, token, CancellationToken.None); }
                    catch (Exception) { /* lock expires on its own */ }
                }
            }
        }
        finally
        {
            gate.Sem.Release();
            Release(key, gate);
        }
    }

    private async Task<(bool Hit, T? Value)> SafeGetAsync<T>(string key, CancellationToken ct)
    {
        try { return await _backend.TryGetAsync<T>(key, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return (false, default); }
    }

    private static Gate Acquire(string key)
    {
        lock (Gates)
        {
            if (!Gates.TryGetValue(key, out var g)) Gates[key] = g = new Gate();
            g.Refs++;
            return g;
        }
    }

    private static void Release(string key, Gate g)
    {
        lock (Gates)
        {
            if (--g.Refs == 0) Gates.Remove(key);
        }
    }
}
