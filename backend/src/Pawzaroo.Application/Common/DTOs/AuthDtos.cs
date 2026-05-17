namespace Pawzaroo.Application.Common.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName, string? PhoneNumber);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserSummary User);
public record UserSummary(Guid Id, string Email, string DisplayName, string? AvatarUrl, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
