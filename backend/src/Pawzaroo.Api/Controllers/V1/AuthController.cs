using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pawzaroo.Application.Common.Cqrs;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Identity.Features.Login;
using Pawzaroo.Application.Modules.Identity.Features.Refresh;
using Pawzaroo.Application.Modules.Identity.Features.Register;

namespace Pawzaroo.Api.Controllers.V1;

public record RegisterDto(string Email, string Password, string DisplayName, string? PhoneNumber);
public record LoginDto(string Email, string Password, string? TwoFactorCode = null, string? DeviceFingerprint = null);
public record RefreshDto(string RefreshToken);

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _current;

    public AuthController(IMediator mediator, ICurrentUserService current)
    {
        _mediator = mediator;
        _current = current;
    }

    [HttpPost("register")]
    public Task<AuthResponse> Register([FromBody] RegisterDto dto, CancellationToken ct)
        => _mediator.SendAsync(new RegisterCommand(dto.Email, dto.Password, dto.DisplayName, dto.PhoneNumber,
            HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

    [HttpPost("login")]
    public Task<AuthResponse> Login([FromBody] LoginDto dto, CancellationToken ct)
        => _mediator.SendAsync(new LoginCommand(
            dto.Email,
            dto.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            dto.DeviceFingerprint,
            dto.TwoFactorCode), ct);

    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh([FromBody] RefreshDto dto, CancellationToken ct)
        => _mediator.SendAsync(new RefreshTokenCommand(dto.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString()), ct);

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshDto dto, CancellationToken ct)
    {
        await _mediator.SendAsync(new LogoutCommand(dto.RefreshToken), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        userId = _current.UserId,
        email = _current.Email,
        permissions = _current.Permissions
    });
}
