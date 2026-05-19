using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly IProductReviewService _reviews;
    private readonly IInventoryService _inventory;
    private readonly IProductCategoryService _categories;

    public ProductsController(IProductService products, IProductReviewService reviews,
        IInventoryService inventory, IProductCategoryService categories)
    {
        _products = products;
        _reviews = reviews;
        _inventory = inventory;
        _categories = categories;
    }

    // ----- Catalog (public) ---------------------------------------------------

    [HttpGet]
    [AllowAnonymous]
    public Task<PageResult<ProductSummaryDto>> Search(
        [FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] Guid? brandId,
        [FromQuery] Guid? storeId, [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
        [FromQuery] bool? featured, [FromQuery] bool? inStockOnly, [FromQuery] string? sort,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24, CancellationToken ct = default)
        => _products.SearchAsync(new ProductSearchQuery(
            ProductListingScope.Public, search, categoryId, brandId, storeId,
            minPrice, maxPrice, featured, inStockOnly, sort, page, pageSize), ct);

    [HttpGet("mine")]
    [Authorize]
    public Task<PageResult<ProductSummaryDto>> Mine(
        [FromQuery] string? search, [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _products.SearchAsync(new ProductSearchQuery(
            ProductListingScope.MyStore, search, categoryId, null, null,
            null, null, null, null, "newest", page, pageSize), ct);

    [HttpGet("admin/all")]
    [Permission(Permissions.Products.Edit)]
    public Task<PageResult<ProductSummaryDto>> AdminAll(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _products.SearchAsync(new ProductSearchQuery(
            ProductListingScope.AdminAll, search, null, null, null, null, null, null, null, "newest", page, pageSize), ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(id, ct);
        return p is null ? NotFound() : Ok(p);
    }

    // ----- Owner CRUD ---------------------------------------------------------

    [HttpPost]
    [Authorize]
    [Permission(Permissions.Products.Create)]
    public async Task<IActionResult> Create([FromBody] CreateProductInput input, CancellationToken ct)
    {
        var id = await _products.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id, version = "1.0" }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductInput input, CancellationToken ct)
    {
        await _products.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/feature")]
    [Permission(Permissions.Products.Feature)]
    public async Task<IActionResult> Feature(Guid id, [FromQuery] bool value = true, CancellationToken ct = default)
    {
        await _products.SetFeaturedAsync(id, value, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize]
    public async Task<IActionResult> Publish(Guid id, [FromQuery] bool active = true, CancellationToken ct = default)
    {
        await _products.SetActiveAsync(id, active, ct);
        return NoContent();
    }

    // ----- Inventory ----------------------------------------------------------

    [HttpPost("{id:guid}/inventory")]
    [Authorize]
    public async Task<ActionResult<InventoryAdjustmentDto>> Adjust(Guid id, [FromBody] AdjustInventoryInput input, CancellationToken ct)
        => Ok(await _inventory.AdjustAsync(id, input, ct));

    [HttpGet("{id:guid}/inventory")]
    [Authorize]
    public Task<PageResult<InventoryAdjustmentDto>> Inventory(Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => _inventory.ListAsync(id, page, pageSize, ct);

    // ----- Reviews ------------------------------------------------------------

    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    public Task<PageResult<ProductReviewDto>> ListReviews(Guid id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => _reviews.ListAsync(id, page, pageSize, ct);

    [HttpPost("{id:guid}/reviews")]
    [Authorize]
    public Task<ProductReviewDto> CreateReview(Guid id, [FromBody] CreateProductReviewInput input, CancellationToken ct)
        => _reviews.CreateAsync(id, input, ct);

    [HttpPut("reviews/{reviewId:guid}")]
    [Authorize]
    public Task<ProductReviewDto> UpdateReview(Guid reviewId, [FromBody] UpdateProductReviewInput input, CancellationToken ct)
        => _reviews.UpdateAsync(reviewId, input, ct);

    [HttpDelete("reviews/{reviewId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(Guid reviewId, CancellationToken ct)
    {
        await _reviews.DeleteAsync(reviewId, ct);
        return NoContent();
    }

    // ----- Categories ---------------------------------------------------------

    [HttpGet("categories")]
    [AllowAnonymous]
    public Task<IReadOnlyList<ProductCategoryDto>> Categories(CancellationToken ct) => _categories.ListAsync(ct);

    [HttpPost("categories")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateProductCategoryInput input, CancellationToken ct)
    {
        var id = await _categories.CreateAsync(input, ct);
        return Ok(new { id });
    }

    [HttpPut("categories/{id:guid}")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateProductCategoryInput input, CancellationToken ct)
    {
        await _categories.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("categories/{id:guid}")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
