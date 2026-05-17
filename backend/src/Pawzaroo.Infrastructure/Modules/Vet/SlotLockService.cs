using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Vet.Services;
using Pawzaroo.Shared.Exceptions;
using StackExchange.Redis;

namespace Pawzaroo.Infrastructure.Modules.Vet;

/// <summary>
/// Redis SET-NX-EX distributed lock keyed by `lock:slot:{doctorId}:{slotId}`.
/// Booking flow acquires the lock for ~15s while the DB transaction runs;
/// failure to acquire signals a concurrent booking attempt — caller gets a
/// 409 Conflict.
/// </summary>
public class SlotLockService : ISlotLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SlotLockService> _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    public SlotLockService(IConnectionMultiplexer redis, ILogger<SlotLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IAsyncDisposable> AcquireAsync(Guid doctorId, Guid slotId, TimeSpan timeout, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"lock:slot:{doctorId}:{slotId}";
        var token = Guid.NewGuid().ToString("N");
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await db.StringSetAsync(key, token, Ttl, When.NotExists))
                    return new Releaser(db, key, token, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SET NX failed; falling back to optimistic mode");
                return new Releaser(null, key, token, _logger); // best-effort
            }
            await Task.Delay(50, ct);
        }
        throw new ConflictException("Slot is being booked by someone else. Try again.");
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly IDatabase? _db;
        private readonly string _key;
        private readonly string _token;
        private readonly ILogger _logger;
        public Releaser(IDatabase? db, string key, string token, ILogger logger)
        { _db = db; _key = key; _token = token; _logger = logger; }

        public async ValueTask DisposeAsync()
        {
            if (_db is null) return;
            try
            {
                // Lua to release only if we still own it.
                const string lua = """
                if redis.call("get", KEYS[1]) == ARGV[1] then
                  return redis.call("del", KEYS[1])
                else return 0 end
                """;
                await _db.ScriptEvaluateAsync(lua, new RedisKey[] { _key }, new RedisValue[] { _token });
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Slot lock release failed"); }
        }
    }
}
