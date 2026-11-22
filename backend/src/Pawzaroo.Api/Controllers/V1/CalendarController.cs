using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Modules.VetPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>iCalendar (.ics) export of appointments. Requires the bearer token like every other endpoint.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/calendar")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly ICalendarExportService _svc;
    public CalendarController(ICalendarExportService svc) => _svc = svc;

    [HttpGet("appointments/{id:guid}.ics")]
    [Produces("text/calendar")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> One(Guid id, CancellationToken ct)
        => File(Encoding.UTF8.GetBytes(await _svc.AppointmentIcsAsync(id, ct)), "text/calendar; charset=utf-8", $"appointment-{id:N}.ics");

    [HttpGet("upcoming.ics")]
    [Produces("text/calendar")]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> Upcoming(CancellationToken ct)
        => File(Encoding.UTF8.GetBytes(await _svc.UpcomingIcsAsync(ct)), "text/calendar; charset=utf-8", "pawzaroo-upcoming.ics");
}
