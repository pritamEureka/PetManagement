using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Modules.VetPlus;

namespace Pawzaroo.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/appointment-series")]
[Authorize]
public class AppointmentSeriesController : ControllerBase
{
    private readonly IRecurringBookingService _svc;
    public AppointmentSeriesController(IRecurringBookingService svc) => _svc = svc;

    [HttpPost]
    [Pawzaroo.Api.Filters.Idempotent]
    [EnableRateLimiting("writes")]
    public Task<BookSeriesResult> Book([FromBody] BookSeriesInput input, CancellationToken ct) => _svc.BookSeriesAsync(input, ct);
}
