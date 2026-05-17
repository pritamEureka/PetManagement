namespace Pawzaroo.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public interface IKafkaProducer
{
    Task PublishAsync<T>(string topic, T message, string? key = null, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyUserAsync(Guid userId, string title, string body, object? payload = null, CancellationToken ct = default);
    Task BroadcastAsync(string title, string body, object? payload = null, CancellationToken ct = default);
}

public interface IPermissionResolver
{
    Task<IReadOnlyCollection<string>> GetPermissionsForUserAsync(Guid userId, CancellationToken ct = default);
}
