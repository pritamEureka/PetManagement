using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Application.Common.Interfaces;

/// <summary>
/// Builds an AuthResponse for the given user: loads roles + permissions, signs
/// the access token, mints + persists a refresh token, primes the Redis perm cache.
/// </summary>
public interface ITokenIssuer
{
    Task<AuthResponse> IssueAsync(User user, string? ip, CancellationToken ct);
}
