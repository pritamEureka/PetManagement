using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Common;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Read-only listing of every appointment for the admin queue. Mutations
/// (cancel / reschedule / refund) go through <c>AppointmentsController</c>
/// or the moderator surface — this endpoint exists so admins can browse.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/appointments")]
[Authorize]
public class AdminAppointmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public AdminAppointmentsController(ApplicationDbContext db) => _db = db;

    public record AppointmentRow(
        Guid Id, Guid DoctorId, string DoctorName,
        Guid UserId, string UserDisplayName,
        DateTime ScheduledAt, AppointmentStatus Status,
        PaymentStatus PaymentStatus, decimal Amount,
        DateTime CreatedAt);

    public record AppointmentListResponse(IReadOnlyList<AppointmentRow> Items, long Total);

    [HttpGet]
    [Permission(Permissions.Appointments.View)]
    public async Task<AppointmentListResponse> List(
        [FromQuery] AppointmentStatus? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var qry = _db.Appointments.AsNoTracking().AsQueryable();
        if (status.HasValue) qry = qry.Where(a => a.Status == status);
        if (from.HasValue)   qry = qry.Where(a => a.ScheduledAt >= from);
        if (to.HasValue)     qry = qry.Where(a => a.ScheduledAt <  to);
        if (!string.IsNullOrWhiteSpace(q))
            qry = qry.Where(a => a.PatientUser.Email.Contains(q)
                              || a.PatientUser.DisplayName.Contains(q)
                              || a.Doctor.User.DisplayName.Contains(q));

        var total = await qry.LongCountAsync(ct);
        var items = await qry.OrderByDescending(a => a.ScheduledAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AppointmentRow(
                a.Id, a.DoctorId, a.Doctor.User.DisplayName,
                a.PatientUserId, a.PatientUser.DisplayName,
                a.ScheduledAt, a.Status, a.PaymentStatus, a.Amount, a.CreatedAt))
            .ToListAsync(ct);
        return new AppointmentListResponse(items, total);
    }
}
