using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>Sessions, backup recovery codes, consent history and step-up auth for the signed-in user.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/security-plus")]
[Authorize]
public class SecurityPlusController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IRecoveryCodeService _recovery;
    private readonly IConsentHistoryService _consents;
    private readonly IStepUpAuthService _stepUp;
    private readonly IPasskeyService _passkeys;
    private readonly IAccountRecoveryService _accountRecovery;

    public SecurityPlusController(ISessionService sessions, IRecoveryCodeService recovery,
        IConsentHistoryService consents, IStepUpAuthService stepUp,
        IPasskeyService passkeys, IAccountRecoveryService accountRecovery)
    {
        _sessions = sessions; _recovery = recovery; _consents = consents; _stepUp = stepUp;
        _passkeys = passkeys; _accountRecovery = accountRecovery;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // --- Passkeys (WebAuthn)
    [HttpPost("passkeys/register/options")]
    public Task<object> PasskeyRegisterOptions(CancellationToken ct) => _passkeys.RegisterOptionsAsync(ct);

    [HttpPost("passkeys/register/verify")]
    public Task<PasskeyDto> PasskeyRegisterVerify([FromBody] PasskeyRegisterVerifyInput input, CancellationToken ct)
        => _passkeys.RegisterVerifyAsync(input, ct);

    [HttpGet("passkeys")]
    public Task<IReadOnlyList<PasskeyDto>> Passkeys(CancellationToken ct) => _passkeys.ListAsync(ct);

    [HttpDelete("passkeys/{id:guid}")]
    public async Task<IActionResult> DeletePasskey(Guid id, CancellationToken ct)
    {
        await _passkeys.DeleteAsync(id, ct);
        return NoContent();
    }

    [AllowAnonymous, EnableRateLimiting("auth")]
    [HttpPost("passkeys/login/options")]
    public Task<object> PasskeyLoginOptions([FromBody] PasskeyLoginOptionsInput input, CancellationToken ct)
        => _passkeys.LoginOptionsAsync(input.Email, ct);

    [AllowAnonymous, EnableRateLimiting("auth")]
    [HttpPost("passkeys/login/verify")]
    public Task<AuthResponse> PasskeyLoginVerify([FromBody] PasskeyLoginVerifyInput input, CancellationToken ct)
        => _passkeys.LoginVerifyAsync(input, Ip, Request.Headers.UserAgent.ToString(), ct);

    // --- Account recovery (anonymous)
    /// <summary>Always returns 202 with the same body, whether or not the account exists.</summary>
    [AllowAnonymous, EnableRateLimiting("auth")]
    [HttpPost("recovery/start")]
    public async Task<IActionResult> RecoveryStart([FromBody] RecoveryStartInput input, CancellationToken ct)
    {
        await _accountRecovery.StartAsync(input.Email, Ip, ct);
        return Accepted(new { message = "If an account exists for that email, a recovery link has been sent." });
    }

    [AllowAnonymous, EnableRateLimiting("auth")]
    [HttpPost("recovery/complete")]
    public async Task<IActionResult> RecoveryComplete([FromBody] RecoveryCompleteInput input, CancellationToken ct)
    {
        await _accountRecovery.CompleteAsync(input, Ip, Request.Headers.UserAgent.ToString(), ct);
        return Ok(new { reset = true });
    }

    // --- Sessions
    [HttpGet("sessions")]
    public Task<IReadOnlyList<SessionDto>> Sessions(CancellationToken ct) => _sessions.ListAsync(ct);

    public record CurrentSessionRequest(string? RefreshToken);

    /// <summary>Which listed session is this browser? POST (not GET) so the refresh token never lands in a URL or log.</summary>
    [HttpPost("sessions/current")]
    public async Task<IActionResult> CurrentSession([FromBody] CurrentSessionRequest body, CancellationToken ct)
        => Ok(new { id = await _sessions.FindCurrentAsync(body.RefreshToken ?? "", ct) });

    [HttpPost("sessions/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _sessions.RevokeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
        => Ok(new { revoked = await _sessions.RevokeAllAsync(ct) });

    public record TrustBody(bool Trusted);

    [HttpPut("devices/{id:guid}/trusted")]
    public async Task<IActionResult> SetTrusted(Guid id, [FromBody] TrustBody body, CancellationToken ct)
    {
        await _sessions.SetDeviceTrustedAsync(id, body.Trusted, ct);
        return NoContent();
    }

    // --- Step-up
    [HttpPost("step-up")]
    public async Task<IActionResult> StepUp([FromBody] StepUpInput input, CancellationToken ct)
    {
        if (!await _stepUp.VerifyAsync(input, ct)) throw new ForbiddenException("Verification failed.");
        return Ok(new { verified = true, validForSeconds = (int)IStepUpAuthService.Window.TotalSeconds });
    }

    // --- Recovery codes
    [HttpGet("recovery-codes")]
    public Task<RecoveryStatusDto> RecoveryStatus(CancellationToken ct) => _recovery.StatusAsync(ct);

    /// <summary>Generates/regenerates codes. Requires a recent step-up verification.</summary>
    [HttpPost("recovery-codes/regenerate")]
    public async Task<RecoveryCodesDto> RegenerateRecovery(CancellationToken ct)
    {
        await _stepUp.RequireRecentAsync(ct);
        return await _recovery.GenerateAsync(ct);
    }

    // --- Consent history
    [HttpGet("consents")]
    public Task<IReadOnlyList<ConsentDto>> Consents(CancellationToken ct) => _consents.ListMineAsync(ct);

    [HttpPost("consents")]
    public Task<ConsentDto> RecordConsent([FromBody] RecordConsentInput input, CancellationToken ct)
        => _consents.RecordAsync(input, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(), ct);
}
