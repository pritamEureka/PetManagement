using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Services;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/shipping-addresses")]
[Authorize]
public class ShippingAddressesController : ControllerBase
{
    private readonly IShippingAddressService _svc;
    public ShippingAddressesController(IShippingAddressService svc) => _svc = svc;

    [HttpGet]
    public Task<IReadOnlyList<ShippingAddressDto>> Mine(CancellationToken ct) => _svc.ListMineAsync(ct);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertShippingAddressInput input, CancellationToken ct)
    {
        var id = await _svc.CreateAsync(input, ct);
        return Ok(new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertShippingAddressInput input, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, input, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
    {
        await _svc.SetDefaultAsync(id, ct);
        return NoContent();
    }
}
