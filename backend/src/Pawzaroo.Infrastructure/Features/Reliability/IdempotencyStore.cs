using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pawzaroo.Application.Modules.Reliability;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Features.Reliability;

/// <summary>
/// DB-backed idempotency store. Every operation runs on its OWN DbContext (fresh scope) so it can never flush,
/// or be poisoned by, half-tracked state of the request being protected.
/// </summary>
public class IdempotencyStore : IIdempotencyStore
{
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    /// <summary>A Started row older than this is considered abandoned (crashed request) and can be reclaimed.</summary>
    public static readonly TimeSpan StaleStartedAfter = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    public IdempotencyStore(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<IdempotencyBeginResult> BeginAsync(Guid userId, string key, string method, string path,
        string requestHash, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            var rec = new IdempotencyRecord
            {
                Key = key, UserId = userId, Method = method, Path = path, RequestHash = requestHash,
                Status = IdempotencyStatus.Started, CreatedAt = now, ExpiresAt = now + Ttl
            };
            db.Set<IdempotencyRecord>().Add(rec);
            try
            {
                await db.SaveChangesAsync(ct);
                return new IdempotencyBeginResult(IdempotencyBeginOutcome.Proceed, rec.Id, null, null, null);
            }
            catch (DbUpdateException)
            {
                db.Entry(rec).State = EntityState.Detached;
            }

            var existing = await db.Set<IdempotencyRecord>().AsNoTracking().FirstOrDefaultAsync(
                r => r.UserId == userId && r.Key == key && r.Method == method && r.Path == path, ct);
            if (existing is null) continue; // removed between insert and read - retry

            if (existing.ExpiresAt <= now)
            {
                await db.Set<IdempotencyRecord>().Where(r => r.Id == existing.Id && r.ExpiresAt <= now).ExecuteDeleteAsync(ct);
                continue;
            }
            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                return new IdempotencyBeginResult(IdempotencyBeginOutcome.Mismatch, existing.Id, null, null, null);

            if (existing.Status == IdempotencyStatus.Completed)
                return new IdempotencyBeginResult(IdempotencyBeginOutcome.Replay, existing.Id,
                    existing.ResponseStatusCode, existing.ResponseBody, existing.ContentType);

            if (existing.CreatedAt <= now - StaleStartedAfter)
            {
                await db.Set<IdempotencyRecord>().Where(r => r.Id == existing.Id && r.Status == IdempotencyStatus.Started)
                    .ExecuteDeleteAsync(ct);
                continue;
            }
            return new IdempotencyBeginResult(IdempotencyBeginOutcome.InFlight, existing.Id, null, null, null);
        }
        // Could not settle after retries: be conservative and tell the client to retry shortly.
        return new IdempotencyBeginResult(IdempotencyBeginOutcome.InFlight, Guid.Empty, null, null, null);
    }

    public async Task CompleteAsync(Guid recordId, int statusCode, string? body, string? contentType, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Set<IdempotencyRecord>().Where(r => r.Id == recordId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, IdempotencyStatus.Completed)
            .SetProperty(r => r.ResponseStatusCode, statusCode)
            .SetProperty(r => r.ResponseBody, body)
            .SetProperty(r => r.ContentType, contentType), ct);
    }

    public async Task AbandonAsync(Guid recordId, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Set<IdempotencyRecord>().Where(r => r.Id == recordId && r.Status == IdempotencyStatus.Started)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Removes expired idempotency rows (24h TTL) and abandoned Started rows. Called by IdempotencyCleanupJob.</summary>
public class IdempotencyCleanupService : IIdempotencyCleanupService
{
    private readonly IServiceScopeFactory _scopes;
    public IdempotencyCleanupService(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<int> CleanupExpiredAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var staleStarted = now - IdempotencyStore.StaleStartedAfter;
        return await db.Set<IdempotencyRecord>()
            .Where(r => r.ExpiresAt <= now
                        || (r.Status == IdempotencyStatus.Started && r.CreatedAt <= staleStarted))
            .ExecuteDeleteAsync(ct);
    }
}
