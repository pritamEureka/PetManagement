using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

/// <summary>
/// Marketplace Redis cache. Two kinds of cache:
///   - Hot product first-page (variant key = serialized filters) — short TTL.
///   - Per-store header cache — invalidated on update.
/// Wrap product writes with InvalidateProductsAsync, store edits with InvalidateStoreAsync.
/// </summary>
public class MarketplaceCache : IMarketplaceCache
{
    private readonly IRedisCacheService _cache;

    private static readonly TimeSpan FirstPageTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StoreTtl     = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InvalidatedTtl = TimeSpan.FromSeconds(1);

    private const string ProductFirstPagePrefix = "marketplace:products:first-page:";
    private const string ProductFirstPageVersionKey = "marketplace:products:version";
    private const string StorePrefix = "marketplace:store:";

    public MarketplaceCache(IRedisCacheService cache) => _cache = cache;

    public async Task<PageResult<ProductSummaryDto>?> GetProductFirstPageAsync(string variantKey, CancellationToken ct = default)
    {
        var version = await _cache.GetAsync<long?>(ProductFirstPageVersionKey, ct) ?? 0;
        var key = $"{ProductFirstPagePrefix}{version}:{variantKey}";
        return await _cache.GetAsync<PageResult<ProductSummaryDto>>(key, ct);
    }

    public async Task SetProductFirstPageAsync(string variantKey, PageResult<ProductSummaryDto> page, CancellationToken ct = default)
    {
        var version = await _cache.GetAsync<long?>(ProductFirstPageVersionKey, ct) ?? 0;
        var key = $"{ProductFirstPagePrefix}{version}:{variantKey}";
        await _cache.SetAsync(key, page, FirstPageTtl, ct);
    }

    /// <summary>
    /// Bumps the version so every cached first-page key becomes unreachable.
    /// Cheaper than enumerating Redis keys and survives a partial outage.
    /// </summary>
    public async Task InvalidateProductsAsync(CancellationToken ct = default)
    {
        var current = await _cache.GetAsync<long?>(ProductFirstPageVersionKey, ct) ?? 0;
        await _cache.SetAsync(ProductFirstPageVersionKey, current + 1, TimeSpan.FromDays(30), ct);
    }

    public Task<StoreDto?> GetStoreAsync(Guid storeId, CancellationToken ct = default)
        => _cache.GetAsync<StoreDto>($"{StorePrefix}{storeId}", ct);

    public Task SetStoreAsync(StoreDto store, CancellationToken ct = default)
        => _cache.SetAsync($"{StorePrefix}{store.Id}", store, StoreTtl, ct);

    public Task InvalidateStoreAsync(Guid storeId, CancellationToken ct = default)
        => _cache.RemoveAsync($"{StorePrefix}{storeId}", ct);
}
