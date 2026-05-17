// Legacy non-versioned controllers replaced by V1 controllers:
//   - Pawzaroo.Api.Controllers.V1.StoresController
//   - Pawzaroo.Api.Controllers.V1.ProductsController
//   - Pawzaroo.Api.Controllers.V1.OrdersController
//   - Pawzaroo.Api.Controllers.V1.CartController
//   - Pawzaroo.Api.Controllers.V1.ShippingAddressesController
//   - Pawzaroo.Api.Controllers.V1.CommissionsController
//
// The unversioned aliases below keep older callers working until the frontend
// switches to /api/v1/* paths.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsLegacyController : ControllerBase
{
    private readonly IProductService _products;
    public ProductsLegacyController(IProductService products) => _products = products;

    [HttpGet]
    [AllowAnonymous]
    public Task<PageResult<ProductSummaryDto>> List(
        [FromQuery] string? q, [FromQuery] Guid? categoryId, [FromQuery] Guid? brandId,
        [FromQuery] bool? featured, [FromQuery] int page = 1, [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
        => _products.SearchAsync(new ProductSearchQuery(
            ProductListingScope.Public, q, categoryId, brandId, null, null, null, featured,
            null, null, page, pageSize), ct);
}

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersLegacyController : ControllerBase
{
    private readonly IOrderService _orders;
    public OrdersLegacyController(IOrderService orders) => _orders = orders;

    [HttpPost("checkout")]
    public Task<OrderDto> Checkout([FromBody] CheckoutInput input, CancellationToken ct) => _orders.CheckoutAsync(input, ct);

    [HttpGet("mine")]
    public Task<PageResult<OrderDto>> Mine(CancellationToken ct) => _orders.ListMineAsync(1, 50, ct);
}
