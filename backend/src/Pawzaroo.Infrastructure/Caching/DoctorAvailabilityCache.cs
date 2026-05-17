using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Hot path for the vet "Book an appointment" page: the open-slots query for a
/// (doctor, day) is read often and changes only when the doctor edits
/// availability or someone books. Short TTL keeps drift bounded; explicit
/// invalidation is wired into <c>DoctorAvailabilityService</c> mutations.
/// </summary>
public class DoctorAvailabilityCache : IDoctorAvailabilityCache
{
    private readonly CacheHelper _cache;
    public DoctorAvailabilityCache(CacheHelper cache) => _cache = cache;

    public Task<T?> GetSlotsAsync<T>(Guid doctorId, DateOnly date, CancellationToken ct = default)
        => _cache.GetAsync<T>(RedisKeys.DoctorAvailability(doctorId, date));

    public Task SetSlotsAsync<T>(Guid doctorId, DateOnly date, T slots, CancellationToken ct = default)
        => _cache.SetAsync(RedisKeys.DoctorAvailability(doctorId, date), slots, RedisTtls.DoctorAvailability);

    public Task InvalidateForDoctorAsync(Guid doctorId, CancellationToken ct = default)
        => _cache.RemoveByPatternAsync($"{RedisKeys.Vet}:availability:{doctorId}:*");
}
