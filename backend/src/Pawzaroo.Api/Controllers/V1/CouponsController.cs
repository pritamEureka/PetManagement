using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/coupons")]
[Authorize]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _coupons;

    public CouponsController(ICouponService coupons) { _coupons = coupons; }

    /// <summary>Used by CartPage to preview a discount before checkout.</summary>
    [HttpPost("apply")]
    public Task<ApplyCouponResult> Apply([FromBody] ApplyCouponInput input, CancellationToken ct) =>
        _coupons.ApplyAsync(input, ct);

    [HttpGet]
    public Task<IReadOnlyList<CouponDto>> List(CancellationToken ct) => _coupons.ListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] UpsertCouponInput input, CancellationToken ct)
    {
        var id = await _coupons.CreateAsync(input, ct);
        return Ok(new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCouponInput input, CancellationToken ct)
    {
        await _coupons.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _coupons.DeleteAsync(id, ct);
        return NoContent();
    }
}
