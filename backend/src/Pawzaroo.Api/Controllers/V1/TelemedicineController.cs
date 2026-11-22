using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Modules.VetPlus;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Telemedicine rooms, lobby and SOAP notes. Access is limited to the appointment's patient and doctor.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/telemedicine")]
[Authorize]
public class TelemedicineController : ControllerBase
{
    private readonly ITelemedicineService _svc;
    public TelemedicineController(ITelemedicineService svc) => _svc = svc;

    [HttpGet("appointments/{id:guid}/room")]
    public Task<TelemedicineRoomDto> Room(Guid id, CancellationToken ct) => _svc.GetRoomAsync(id, ct);

    [HttpPost("appointments/{id:guid}/check-in")]
    public Task<TelemedicineRoomDto> CheckIn(Guid id, [FromBody] TelemedicineCheckInInput input, CancellationToken ct)
        => _svc.CheckInAsync(id, input, ct);

    [HttpPost("appointments/{id:guid}/admit")]
    public async Task<IActionResult> Admit(Guid id, CancellationToken ct)
    {
        await _svc.AdmitAsync(id, ct);
        return NoContent();
    }

    [HttpPost("appointments/{id:guid}/events")]
    public async Task<IActionResult> Event(Guid id, [FromBody] TelemedicineEventInput input, CancellationToken ct)
    {
        await _svc.RecordEventAsync(id, input.Kind, ct);
        return NoContent();
    }

    [HttpGet("waiting-room")]
    public Task<IReadOnlyList<WaitingPatientDto>> WaitingRoom(CancellationToken ct) => _svc.WaitingListAsync(ct);

    [HttpGet("appointments/{id:guid}/notes")]
    public Task<ConsultationNoteDto> GetNote(Guid id, CancellationToken ct) => _svc.GetNoteAsync(id, ct);

    [HttpPut("appointments/{id:guid}/notes")]
    public Task<ConsultationNoteDto> SaveNote(Guid id, [FromBody] SaveConsultationNoteInput input, CancellationToken ct)
        => _svc.SaveNoteAsync(id, input, ct);
}
