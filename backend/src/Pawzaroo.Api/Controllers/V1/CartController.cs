using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cart;
    public CartController(ICartService cart) => _cart = cart;

    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) => _cart.GetMineAsync(ct);

    [HttpPost("items")]
    public Task<CartDto> Add([FromBody] AddToCartInput input, CancellationToken ct) => _cart.AddAsync(input, ct);

    [HttpPut("items/{id:guid}")]
    public Task<CartDto> UpdateItem(Guid id, [FromBody] UpdateCartItemInput input, CancellationToken ct)
        => _cart.UpdateItemAsync(id, input, ct);

    [HttpDelete("items/{id:guid}")]
    public Task<CartDto> RemoveItem(Guid id, CancellationToken ct) => _cart.RemoveItemAsync(id, ct);

    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _cart.ClearAsync(ct);
        return NoContent();
    }
}
