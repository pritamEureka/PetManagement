using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pawzaroo.Application.Common.DTOs;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.SecurityPlus.WebAuthnCrypto;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

/// <summary>
/// WebAuthn passkeys. Config: Passkeys:RpId (default "localhost"), Passkeys:RpName,
/// Passkeys:Origins (array or comma-separated; defaults to the local dev origins).
/// Challenges live in Redis for 5 minutes and are consumed atomically (GETDEL) on first use.
/// A passkey login always requires user verification, so it satisfies 2FA on its own.
/// </summary>
public class PasskeyService : IPasskeyService
{
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);
    // Per-process secret so that "fake" credential ids for unknown emails look like real ones.
    private static readonly byte[] FakeIdKey = RandomNumberGenerator.GetBytes(32);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly CacheHelper _cache;
    private readonly IAuditLogger _audit;
    private readonly ITokenIssuer _tokens;
    private readonly IUserDeviceService _devices;
    private readonly string _rpId;
    private readonly string _rpName;
    private readonly string[] _origins;

    public PasskeyService(ApplicationDbContext db, ICurrentUserService current, CacheHelper cache,
        IAuditLogger audit, ITokenIssuer tokens, IUserDeviceService devices, IConfiguration config)
    {
        _db = db; _current = current; _cache = cache; _audit = audit; _tokens = tokens; _devices = devices;
        _rpId = config["Passkeys:RpId"] is { Length: > 0 } rp ? rp : "localhost";
        _rpName = config["Passkeys:RpName"] is { Length: > 0 } rn ? rn : "Pawzaroo";

        var origins = new List<string>();
        var section = config.GetSection("Passkeys:Origins");
        foreach (var c in section.GetChildren())
            if (!string.IsNullOrWhiteSpace(c.Value)) origins.AddRange(c.Value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (!string.IsNullOrWhiteSpace(section.Value))
            origins.AddRange(section.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (origins.Count == 0) origins.AddRange(new[] { "http://localhost:5173", "http://localhost:3000" });
        _origins = origins.Select(o => o.TrimEnd('/')).ToArray();
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private record ChallengeState(string Purpose, Guid? UserId);

    private static string ChallengeKey(string challenge) => $"passkey:challenge:{challenge}";

    private async Task<string> NewChallengeAsync(string purpose, Guid? userId)
    {
        var challenge = B64Url(RandomNumberGenerator.GetBytes(32));
        await _cache.SetAsync(ChallengeKey(challenge), new ChallengeState(purpose, userId), ChallengeTtl);
        return challenge;
    }

    /// <summary>Single-use: reads and deletes the challenge in one atomic step.</summary>
    private async Task<ChallengeState> ConsumeChallengeAsync(string challenge, string purpose)
    {
        var raw = await _cache.Db.StringGetDeleteAsync(ChallengeKey(challenge));
        if (raw.IsNullOrEmpty) throw Fail("Challenge expired or already used.");
        ChallengeState? st;
        try
        {
            st = JsonSerializer.Deserialize<ChallengeState>((string)raw!,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch (JsonException) { throw Fail(); }
        if (st is null || st.Purpose != purpose) throw Fail();
        return st;
    }

    // ---------- Registration ----------

    public async Task<object> RegisterOptionsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var user = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid, ct)
                   ?? throw new ForbiddenException();
        var existing = await _db.Set<PasskeyCredential>().AsNoTracking()
            .Where(p => p.UserId == uid).Select(p => p.CredentialId).ToListAsync(ct);
        var challenge = await NewChallengeAsync("register", uid);

        return new
        {
            rp = new { id = _rpId, name = _rpName },
            user = new { id = B64Url(uid.ToByteArray()), name = user.Email, displayName = user.DisplayName },
            challenge,
            pubKeyCredParams = new[] { new { type = "public-key", alg = -7 } },
            timeout = 60000,
            excludeCredentials = existing.Select(id => new { type = "public-key", id }).ToArray(),
            authenticatorSelection = new { residentKey = "preferred", userVerification = "required" },
            attestation = "none"
        };
    }

    public async Task<PasskeyDto> RegisterVerifyAsync(PasskeyRegisterVerifyInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (input?.Response is null || string.IsNullOrEmpty(input.Id)) throw Fail();

        var cdj = FromB64Url(input.Response.ClientDataJSON);
        var cd = ParseClientData(cdj, "webauthn.create", _origins);
        var state = await ConsumeChallengeAsync(cd.Challenge, "register");
        if (state.UserId != uid) throw Fail();

        // attestationObject = CBOR map { fmt, attStmt, authData }; we only need authData.
        var ao = FromB64Url(input.Response.AttestationObject);
        var pos = 0;
        if (Cbor.Read(ao, ref pos) is not Dictionary<object, object> map
            || !map.TryGetValue("authData", out var adObj) || adObj is not byte[] authDataBytes)
            throw Fail();

        var ad = ParseAuthData(authDataBytes, expectCredential: true);
        CheckRpAndFlags(ad, _rpId);

        var credId = B64Url(ad.CredentialId!);
        if (credId != input.Id) throw Fail("Credential id mismatch.");
        var spki = CoseToSpki(ad.CoseKey!);

        if (await _db.Set<PasskeyCredential>().AnyAsync(p => p.CredentialId == credId, ct))
            throw new ConflictException("This passkey is already registered.");

        var label = string.IsNullOrWhiteSpace(input.Label) ? "Passkey" : input.Label.Trim();
        if (label.Length > 100) label = label[..100];
        var row = new PasskeyCredential
        {
            UserId = uid, CredentialId = credId, PublicKey = spki, SignCount = ad.SignCount, Label = label
        };
        _db.Set<PasskeyCredential>().Add(row);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PasskeyRegistered", "PasskeyCredential", row.Id.ToString(), "Security", ct: ct);
        return new PasskeyDto(row.Id, row.Label, row.CreatedAt, row.LastUsedAt);
    }

    public async Task<IReadOnlyList<PasskeyDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await _db.Set<PasskeyCredential>().AsNoTracking().Where(p => p.UserId == uid)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PasskeyDto(p.Id, p.Label, p.CreatedAt, p.LastUsedAt)).ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var uid = Uid();
        var row = await _db.Set<PasskeyCredential>().FirstOrDefaultAsync(p => p.Id == id && p.UserId == uid, ct)
                  ?? throw new NotFoundException("Passkey", id);
        _db.Set<PasskeyCredential>().Remove(row);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("PasskeyRemoved", "PasskeyCredential", id.ToString(), "Security", ct: ct);
    }

    // ---------- Login ----------

    public async Task<object> LoginOptionsAsync(string email, CancellationToken ct = default)
    {
        var e = (email ?? "").Trim().ToLowerInvariant();
        var user = e.Length == 0 ? null : await _db.Set<User>().AsNoTracking()
            .Where(u => u.Email == e).Select(u => new { u.Id }).FirstOrDefaultAsync(ct);

        var ids = user is null
            ? new List<string>()
            : await _db.Set<PasskeyCredential>().AsNoTracking().Where(p => p.UserId == user.Id)
                .Select(p => p.CredentialId).ToListAsync(ct);
        // Unknown email / no passkeys: answer with a stable fake id so the response shape doesn't leak existence.
        if (ids.Count == 0)
            ids.Add(B64Url(HMACSHA256.HashData(FakeIdKey, Encoding.UTF8.GetBytes(e))));

        var challenge = await NewChallengeAsync("login", user?.Id);
        return new
        {
            challenge,
            timeout = 60000,
            rpId = _rpId,
            allowCredentials = ids.Select(id => new { type = "public-key", id }).ToArray(),
            userVerification = "required"
        };
    }

    public async Task<AuthResponse> LoginVerifyAsync(PasskeyLoginVerifyInput input, string? ip, string? userAgent,
        CancellationToken ct = default)
    {
        if (input?.Response is null || string.IsNullOrEmpty(input.Id)) throw Fail();

        var cdj = FromB64Url(input.Response.ClientDataJSON);
        var cd = ParseClientData(cdj, "webauthn.get", _origins);
        var state = await ConsumeChallengeAsync(cd.Challenge, "login");
        if (state.UserId is null) throw Fail();

        var cred = await _db.Set<PasskeyCredential>().FirstOrDefaultAsync(p => p.CredentialId == input.Id, ct);
        if (cred is null || cred.UserId != state.UserId) throw Fail();

        var adBytes = FromB64Url(input.Response.AuthenticatorData);
        var ad = ParseAuthData(adBytes, expectCredential: false);
        CheckRpAndFlags(ad, _rpId);   // UP + UV enforced

        if (!VerifyAssertion(cred.PublicKey, adBytes, cdj, FromB64Url(input.Response.Signature)))
            throw Fail();

        // Clone detection: counters must strictly increase unless the authenticator never uses them (always 0).
        if ((ad.SignCount != 0 || cred.SignCount != 0) && ad.SignCount <= cred.SignCount)
            throw Fail("Signature counter regression; credential may be cloned.");

        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == cred.UserId, ct) ?? throw Fail();
        switch (user.ApprovalStatus)
        {
            case ApprovalStatus.Pending:
                throw new AppException("registration_pending",
                    "Your registration is awaiting admin approval. You'll receive an email once it's approved.");
            case ApprovalStatus.Rejected:
                throw new AppException("registration_rejected", "Your registration was rejected by an administrator.");
        }
        if (!user.IsActive || user.IsSuspended) throw new ForbiddenException("Account disabled.");

        cred.SignCount = ad.SignCount;
        cred.LastUsedAt = DateTime.UtcNow;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try { await _devices.TrackOnLoginAsync(user.Id, userAgent, ip, input.DeviceFingerprint, ct); }
        catch { /* device hint is non-critical */ }

        await _audit.LogAsync("PasskeyLogin", "User", user.Id.ToString(), "Security", ct: ct);
        return await _tokens.IssueAsync(user, ip, ct);
    }
}
