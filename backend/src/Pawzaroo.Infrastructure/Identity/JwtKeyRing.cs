using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Pawzaroo.Infrastructure.Identity;

/// <summary>
/// Signing-key ring for overlapping JWT key rotation. Tokens are always signed with the CURRENT key
/// (<c>Jwt:SigningKey</c>); <c>Jwt:PreviousSigningKeys</c> (array entries or one comma/semicolon separated string)
/// are accepted for VALIDATION only. Every key gets a <c>kid</c> = first 16 hex chars of SHA-256(key bytes).
/// </summary>
public sealed class JwtKeyRing
{
    public const int MinKeyLength = 32;

    public SymmetricSecurityKey Current { get; }
    public IReadOnlyList<SymmetricSecurityKey> Previous { get; }

    /// <summary>Current key first, then previous keys (de-duplicated by kid).</summary>
    public IReadOnlyList<SymmetricSecurityKey> AllKeys { get; }

    public JwtKeyRing(string currentSigningKey, IEnumerable<string>? previousSigningKeys = null)
    {
        if (string.IsNullOrWhiteSpace(currentSigningKey))
            throw new ArgumentException("Jwt:SigningKey is required.", nameof(currentSigningKey));

        Current = Build(currentSigningKey);
        var prev = new List<SymmetricSecurityKey>();
        var seen = new HashSet<string>(StringComparer.Ordinal) { Current.KeyId };
        foreach (var raw in previousSigningKeys ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var k = Build(raw.Trim());
            if (seen.Add(k.KeyId)) prev.Add(k);
        }
        Previous = prev;
        AllKeys = new[] { Current }.Concat(prev).ToList();
    }

    /// <summary>Resolver for <c>TokenValidationParameters.IssuerSigningKeyResolver</c>.</summary>
    public IssuerSigningKeyResolver Resolver => (token, securityToken, kid, parameters) => ResolveKeys(kid);

    /// <summary>
    /// Keys to try for a token header <c>kid</c>. A token with a kid that matches no key yields none (validation fails);
    /// a token with NO kid (issued before kid support) is tried against every key.
    /// </summary>
    public IEnumerable<SecurityKey> ResolveKeys(string? kid)
    {
        if (string.IsNullOrEmpty(kid)) return AllKeys;
        return AllKeys.Where(k => string.Equals(k.KeyId, kid, StringComparison.Ordinal)).ToList();
    }

    public SigningCredentials CreateSigningCredentials() => new(Current, SecurityAlgorithms.HmacSha256);

    public static string ComputeKid(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant();

    private static SymmetricSecurityKey Build(string key)
        => new(Encoding.UTF8.GetBytes(key)) { KeyId = ComputeKid(key) };

    /// <summary>Reads <c>Jwt:PreviousSigningKeys</c> as indexed array entries and/or a delimited string.</summary>
    public static IReadOnlyList<string> ReadPreviousKeys(IConfiguration config)
    {
        var section = config.GetSection("Jwt:PreviousSigningKeys");
        var result = new List<string>();
        void AddDelimited(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return;
            result.AddRange(v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        AddDelimited(section.Value);
        foreach (var child in section.GetChildren()) AddDelimited(child.Value);
        return result;
    }

    public static JwtKeyRing FromConfiguration(IConfiguration config)
        => new(config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey missing"),
               ReadPreviousKeys(config));
}
