using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

public class SessionService : ISessionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditLogger _audit;

    private readonly IJwtTokenService _jwt;

    public SessionService(ApplicationDbContext db, ICurrentUserService current, IAuditLogger audit, IJwtTokenService jwt)
    {
        _db = db; _current = current; _audit = audit; _jwt = jwt;
    }

    public async Task<Guid?> FindCurrentAsync(string refreshToken, CancellationToken ct = default)
    {
        var uid = Uid();
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var hash = _jwt.HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;
        // Scoped to the caller's own tokens, so a foreign token can never reveal anything.
        return await _db.Set<RefreshToken>().AsNoTracking()
            .Where(t => t.UserId == uid && t.TokenHash == hash && t.RevokedAt == null && t.ExpiresAt > now)
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    public async Task<IReadOnlyList<SessionDto>> ListAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var tokens = await _db.Set<RefreshToken>().AsNoTracking()
            .Where(t => t.UserId == uid && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var devices = await _db.Set<UserDevice>().AsNoTracking()
            .Where(d => d.UserId == uid && !d.IsRevoked)
            .ToListAsync(ct);

        return tokens.Select(t =>
        {
            // Refresh tokens carry no device id; correlate best-effort by IP.
            var d = t.CreatedByIp is null ? null
                : devices.Where(x => x.IpAddress == t.CreatedByIp).OrderByDescending(x => x.LastSeenAt).FirstOrDefault();
            return new SessionDto(t.Id, t.CreatedAt, t.ExpiresAt, t.CreatedByIp,
                d?.Label, d?.Id, d?.IsTrusted ?? false, d?.LastSeenAt);
        }).ToList();
    }

    public async Task RevokeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var uid = Uid();
        var t = await _db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == uid, ct)
                ?? throw new NotFoundException("Session", sessionId);
        t.RevokedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("SessionRevoked", "RefreshToken", sessionId.ToString(), "Security", ct: ct);
    }

    public async Task<int> RevokeAllAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var now = DateTime.UtcNow;
        var tokens = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == uid && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAt = now;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("LogOutEverywhere", "User", uid.ToString(), "Security",
            newValues: new { revoked = tokens.Count }, ct: ct);
        return tokens.Count;
    }

    public async Task SetDeviceTrustedAsync(Guid deviceId, bool trusted, CancellationToken ct = default)
    {
        var uid = Uid();
        var d = await _db.Set<UserDevice>().FirstOrDefaultAsync(x => x.Id == deviceId && x.UserId == uid, ct)
                ?? throw new NotFoundException("UserDevice", deviceId);
        if (d.IsRevoked) throw new ConflictException("Device has been revoked.");
        d.IsTrusted = trusted;
        await _db.SaveChangesAsync(ct);
    }
}

public class RecoveryCodeService : IRecoveryCodeService
{
    private const int Count = 10;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IAuditLogger _audit;

    public RecoveryCodeService(ApplicationDbContext db, ICurrentUserService current, IAuditLogger audit)
    {
        _db = db; _current = current; _audit = audit;
    }

    internal static string Normalize(string code) =>
        new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    internal static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("recovery:" + Normalize(code))));

    public async Task<RecoveryStatusDto> StatusAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var all = await _db.Set<BackupRecoveryCode>().AsNoTracking().Where(c => c.UserId == uid).ToListAsync(ct);
        return new RecoveryStatusDto(all.Count(c => c.UsedAt == null), all.Count,
            all.Count == 0 ? null : all.Max(c => c.CreatedAt));
    }

    public async Task<RecoveryCodesDto> GenerateAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var set = _db.Set<BackupRecoveryCode>();
        var old = await set.Where(c => c.UserId == uid).ToListAsync(ct);
        set.RemoveRange(old);

        var plain = new List<string>(Count);
        for (var i = 0; i < Count; i++)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)); // 10 hex chars
            var pretty = raw.Substring(0, 5) + "-" + raw.Substring(5);
            plain.Add(pretty);
            set.Add(new BackupRecoveryCode { UserId = uid, CodeHash = Hash(pretty) });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("RecoveryCodesRegenerated", "User", uid.ToString(), "Security", ct: ct);
        return new RecoveryCodesDto(plain, Count);
    }

    public async Task<bool> ConsumeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var h = Hash(code);
        var row = await _db.Set<BackupRecoveryCode>()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CodeHash == h && c.UsedAt == null, ct);
        if (row is null) return false;
        row.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public class ConsentHistoryService : IConsentHistoryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public ConsentHistoryService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db; _current = current;
    }

    public async Task<ConsentDto> RecordAsync(RecordConsentInput input, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (string.IsNullOrWhiteSpace(input.ConsentType) || input.ConsentType.Length > 64)
            throw new ValidationException(new Dictionary<string, string[]>
            { ["consentType"] = new[] { "Consent type is required (max 64 chars)." } });

        var row = new ConsentRecord
        {
            UserId = uid,
            ConsentType = input.ConsentType.Trim().ToLowerInvariant(),
            Version = input.Version?.Trim(),
            Granted = input.Granted,
            IpAddress = ip,
            UserAgent = userAgent is { Length: > 512 } ? userAgent.Substring(0, 512) : userAgent
        };
        _db.Set<ConsentRecord>().Add(row);   // append-only: no update/delete paths exist
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<IReadOnlyList<ConsentDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var rows = await _db.Set<ConsentRecord>().AsNoTracking()
            .Where(c => c.UserId == uid).OrderByDescending(c => c.RecordedAt).Take(500).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static ConsentDto ToDto(ConsentRecord c) =>
        new(c.Id, c.ConsentType, c.Version, c.Granted, c.IpAddress, c.RecordedAt);
}

public class StepUpAuthService : IStepUpAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;
    private readonly IOtpService _otp;
    private readonly IRecoveryCodeService _recovery;

    public StepUpAuthService(ApplicationDbContext db, ICurrentUserService current, IPasswordHasher hasher,
        IOtpService otp, IRecoveryCodeService recovery)
    {
        _db = db; _current = current; _hasher = hasher; _otp = otp; _recovery = recovery;
    }

    public async Task<bool> VerifyAsync(StepUpInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        string? method = null;

        if (!string.IsNullOrEmpty(input.Password))
        {
            var hash = await _db.Set<User>().AsNoTracking().Where(u => u.Id == uid)
                .Select(u => u.PasswordHash).FirstOrDefaultAsync(ct);
            if (hash is not null && _hasher.Verify(input.Password, hash)) method = "password";
        }
        else if (!string.IsNullOrWhiteSpace(input.OtpCode))
        {
            var code = input.OtpCode.Trim();
            if (await _otp.VerifyAsync(uid, OtpPurpose.TwoFactor, code, ct)) method = "otp";
            else if (await _recovery.ConsumeAsync(uid, code, ct)) method = "recovery";
        }

        if (method is null) return false;
        _db.Set<StepUpVerification>().Add(new StepUpVerification { UserId = uid, Method = method });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HasRecentAsync(Guid userId, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - IStepUpAuthService.Window;
        return await _db.Set<StepUpVerification>().AsNoTracking()
            .AnyAsync(v => v.UserId == userId && v.VerifiedAt >= since, ct);
    }

    public async Task RequireRecentAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        if (!await HasRecentAsync(uid, ct))
            throw new ForbiddenException("step_up_required");
    }
}
