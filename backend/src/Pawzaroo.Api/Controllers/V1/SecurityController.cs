using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Self-service security surface for the signed-in user:
///   - own device list
///   - own warnings (and acknowledgement)
///   - active suspension (so the SPA can show /account/suspended)
///   - OTP issuance / verification
///   - 2FA setup / enable / disable
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/security")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly ICurrentUserService _current;
    private readonly IUserDeviceService _devices;
    private readonly IUserDisciplineService _discipline;
    private readonly IOtpService _otp;
    private readonly ITwoFactorService _twoFactor;

    public SecurityController(ICurrentUserService current, IUserDeviceService devices,
        IUserDisciplineService discipline, IOtpService otp, ITwoFactorService twoFactor)
    {
        _current = current;
        _devices = devices;
        _discipline = discipline;
        _otp = otp;
        _twoFactor = twoFactor;
    }

    // --- Account state -----------------------------------------------------

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var active = await _discipline.GetActiveAsync(uid, ct);
        var warnings = await _discipline.ListWarningsForUserAsync(uid, ct);
        var twoFactor = await _twoFactor.IsEnabledAsync(uid, ct);
        return Ok(new
        {
            userId = uid,
            email = _current.Email,
            permissions = _current.Permissions,
            twoFactorEnabled = twoFactor,
            activeSuspension = active,
            pendingWarnings = warnings.Where(w => !w.AcknowledgedByUser)
        });
    }

    // --- Devices -----------------------------------------------------------

    [HttpGet("devices")]
    public Task<IReadOnlyList<UserDeviceDto>> Devices(CancellationToken ct) => _devices.ListMineAsync(ct);

    [HttpPost("devices/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeDevice(Guid id, CancellationToken ct)
    {
        await _devices.RevokeAsync(id, ct);
        return NoContent();
    }

    public record TrustDeviceBody(string? Label);

    [HttpPost("devices/{id:guid}/trust")]
    public async Task<IActionResult> TrustDevice(Guid id, [FromBody] TrustDeviceBody body, CancellationToken ct)
    {
        await _devices.TrustAsync(id, body?.Label, ct);
        return NoContent();
    }

    // --- Warnings ----------------------------------------------------------

    [HttpGet("warnings")]
    public async Task<IReadOnlyList<UserWarningDto>> MyWarnings(CancellationToken ct)
        => await _discipline.ListWarningsForUserAsync(
            _current.UserId ?? throw new ForbiddenException(), ct);

    [HttpPost("warnings/{id:guid}/ack")]
    public async Task<IActionResult> Ack(Guid id, CancellationToken ct)
    {
        await _discipline.AcknowledgeWarningAsync(id, ct);
        return NoContent();
    }

    // --- OTP ---------------------------------------------------------------

    [HttpPost("otp/issue")]
    public async Task<IActionResult> IssueOtp([FromBody] StartVerificationInput input, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        await _otp.IssueAsync(uid, input.Purpose, input.Destination, ct);
        return NoContent();
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpInput input, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var ok = await _otp.VerifyAsync(uid, input.Purpose, input.Code, ct);
        return ok ? Ok(new { verified = true }) : BadRequest(new { verified = false });
    }

    // --- 2FA ---------------------------------------------------------------

    [HttpPost("2fa/setup")]
    public Task<TwoFactorSetupDto> Begin2FA(CancellationToken ct) => _twoFactor.BeginSetupAsync(ct);

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> Enable2FA([FromBody] EnableTwoFactorInput body, CancellationToken ct)
    {
        var ok = await _twoFactor.ConfirmEnableAsync(body.Code, ct);
        return ok ? NoContent() : BadRequest(new { error = new { code = "invalid_code", message = "Invalid TOTP code." } });
    }

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> Disable2FA([FromBody] DisableTwoFactorInput body, CancellationToken ct)
    {
        var ok = await _twoFactor.DisableAsync(body.Code, ct);
        return ok ? NoContent() : BadRequest(new { error = new { code = "invalid_code", message = "Invalid TOTP code." } });
    }
}
