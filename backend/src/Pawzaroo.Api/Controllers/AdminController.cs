// Legacy non-versioned shims kept for older callers. New code targets V1:
//   /api/v1/admin/overview       (AdminDashboardController)
//   /api/v1/admin/users          (UsersAdminController)
//   /api/v1/admin/appointments   (AdminAppointmentsController)
//   /api/v1/admin/notifications  (AdminNotificationsController)
//   /api/v1/admin/settings       (AdminSettingsController)
//
// These /api/admin/* aliases proxy to the V1 surface so existing UIs keep
// working through the migration.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Api.Controllers.V1;
using Pawzaroo.Application.Common.Permissions;

namespace Pawzaroo.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AdminDashboardController _dashboard;
    private readonly UsersAdminController _users;

    public AdminController(AdminDashboardController dashboard, UsersAdminController users)
    {
        _dashboard = dashboard;
        _users = users;
    }

    [HttpGet("dashboard")]
    [Permission(Permissions.Users.View)]
    public Task<ActionResult<AdminDashboardController.OverviewDto>> Dashboard(CancellationToken ct) =>
        _dashboard.Overview(ct);

    [HttpGet("users")]
    [Permission(Permissions.Users.View)]
    public Task<UsersAdminController.UserListResponse> Users(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => _users.Search(q, null, null, null, page, pageSize, ct);
}
