using Pawzaroo.Application.Common.DTOs;

namespace Pawzaroo.Application.Modules.SecurityPlus;

public record PasskeyDto(Guid Id, string Label, DateTime CreatedAt, DateTime? LastUsedAt);

/// <summary>Browser credential payload (base64url strings), as produced by navigator.credentials.create().</summary>
public record PasskeyAttestationResponse(string ClientDataJSON, string AttestationObject);
public record PasskeyRegisterVerifyInput(string Id, PasskeyAttestationResponse Response, string? Label);

public record PasskeyAssertionResponse(string ClientDataJSON, string AuthenticatorData, string Signature, string? UserHandle);
public record PasskeyLoginVerifyInput(string Id, PasskeyAssertionResponse Response, string? DeviceFingerprint);

public record PasskeyLoginOptionsInput(string Email);

public interface IPasskeyService
{
    /// <summary>PublicKeyCredentialCreationOptions (JSON form) for the signed-in user. Stores a single-use challenge.</summary>
    Task<object> RegisterOptionsAsync(CancellationToken ct = default);
    Task<PasskeyDto> RegisterVerifyAsync(PasskeyRegisterVerifyInput input, CancellationToken ct = default);
    Task<IReadOnlyList<PasskeyDto>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>PublicKeyCredentialRequestOptions (JSON form). Anonymous; never reveals whether the email exists.</summary>
    Task<object> LoginOptionsAsync(string email, CancellationToken ct = default);
    Task<AuthResponse> LoginVerifyAsync(PasskeyLoginVerifyInput input, string? ip, string? userAgent, CancellationToken ct = default);
}

public record RecoveryStartInput(string Email);
public record RecoveryCompleteInput(string? Token, string? Email, string? RecoveryCode, string NewPassword);

public interface IAccountRecoveryService
{
    /// <summary>Always completes silently (no user enumeration). Sends a time-limited link when the account exists.</summary>
    Task StartAsync(string email, string? ip, CancellationToken ct = default);
    Task CompleteAsync(RecoveryCompleteInput input, string? ip, string? userAgent, CancellationToken ct = default);
}
