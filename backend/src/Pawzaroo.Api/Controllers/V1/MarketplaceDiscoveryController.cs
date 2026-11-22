using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.ShopPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>8.7 — faceted search, suggestions, saved searches, recently viewed, price watches.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/marketplace-discovery")]
[Authorize]
public class MarketplaceDiscoveryController : ControllerBase
{
    private readonly IMarketplaceDiscoveryService _svc;
    public MarketplaceDiscoveryController(IMarketplaceDiscoveryService svc) => _svc = svc;

    [HttpGet("search")]
    [AllowAnonymous]
    public Task<FacetSearchResult> Search([FromQuery] FacetSearchRequest request, CancellationToken ct)
        => _svc.SearchAsync(request, ct);

    [HttpGet("suggest")]
    [AllowAnonymous]
    public Task<IReadOnlyList<SuggestionDto>> Suggest([FromQuery] string q, [FromQuery] int limit = 8, CancellationToken ct = default)
        => _svc.SuggestAsync(q, limit, ct);

    [HttpGet("saved-searches")]
    public Task<IReadOnlyList<SavedSearchDto>> SavedSearches(CancellationToken ct) => _svc.ListSavedSearchesAsync(ct);

    [HttpPost("saved-searches")]
    public Task<SavedSearchDto> CreateSavedSearch([FromBody] CreateSavedSearchInput input, CancellationToken ct)
        => _svc.CreateSavedSearchAsync(input, ct);

    [HttpPut("saved-searches/{id:guid}")]
    public Task<SavedSearchDto> UpdateSavedSearch(Guid id, [FromBody] UpdateSavedSearchInput input, CancellationToken ct)
        => _svc.UpdateSavedSearchAsync(id, input, ct);

    [HttpDelete("saved-searches/{id:guid}")]
    public async Task<IActionResult> DeleteSavedSearch(Guid id, CancellationToken ct)
    {
        await _svc.DeleteSavedSearchAsync(id, ct);
        return NoContent();
    }

    [HttpPost("recently-viewed/{productId:guid}")]
    public async Task<IActionResult> RecordView(Guid productId, CancellationToken ct)
    {
        await _svc.RecordViewAsync(productId, ct);
        return NoContent();
    }

    [HttpGet("recently-viewed")]
    public Task<IReadOnlyList<ProductSummaryDto>> RecentlyViewed([FromQuery] int limit = 12, CancellationToken ct = default)
        => _svc.RecentlyViewedAsync(limit, ct);

    [HttpGet("price-watches")]
    public Task<IReadOnlyList<PriceWatchDto>> PriceWatches(CancellationToken ct) => _svc.ListPriceWatchesAsync(ct);

    [HttpPost("price-watches")]
    public Task<PriceWatchDto> CreatePriceWatch([FromBody] CreatePriceWatchInput input, CancellationToken ct)
        => _svc.CreatePriceWatchAsync(input, ct);

    [HttpDelete("price-watches/{id:guid}")]
    public async Task<IActionResult> DeletePriceWatch(Guid id, CancellationToken ct)
    {
        await _svc.DeletePriceWatchAsync(id, ct);
        return NoContent();
    }

    /// <summary>Store owner: set the geo point used for distance ranking.</summary>
    [HttpPut("my-store-location")]
    public async Task<IActionResult> SetStoreLocation([FromBody] SetStoreLocationInput input, CancellationToken ct)
    {
        await _svc.SetMyStoreLocationAsync(input, ct);
        return NoContent();
    }
}
