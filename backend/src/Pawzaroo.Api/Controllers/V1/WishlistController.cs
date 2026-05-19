using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlist;

    public WishlistController(IWishlistService wishlist) { _wishlist = wishlist; }

    [HttpGet]
    public Task<IReadOnlyList<WishlistItemDto>> Mine(CancellationToken ct) => _wishlist.ListMineAsync(ct);

    [HttpPost("{productId:guid}")]
    public async Task<IActionResult> Add(Guid productId, CancellationToken ct)
    {
        await _wishlist.AddAsync(productId, ct);
        return NoContent();
    }

    [HttpDelete("{productId:guid}")]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
    {
        await _wishlist.RemoveAsync(productId, ct);
        return NoContent();
    }

    [HttpGet("{productId:guid}/status")]
    public async Task<ActionResult<object>> Status(Guid productId, CancellationToken ct)
    {
        var wishlisted = await _wishlist.IsWishlistedAsync(productId, ct);
        return Ok(new { wishlisted });
    }
}
