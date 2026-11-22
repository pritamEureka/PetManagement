using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.SecurityPlus;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

/// <summary>
/// Forgot-password / account recovery. Start is always silent (no enumeration) and rate limited per IP and per email.
/// Complete accepts either an emailed single-use token (30 min) or email + backup recovery code, resets the
/// password, revokes every session and appends a consent/audit record.
/// Config: App:FrontendBaseUrl (falls back to the first Cors:AllowedOrigins entry, then http://localhost:5173).
/// </summary>
public class AccountRecoveryService : IAccountRecoveryService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _db;
    private readonly CacheHelper _cache;
    private readonly IRedisRateLimiter _limiter;
    private readonly IEmailService _email;
    private readonly IPasswordHasher _hasher;
    private readonly IRecoveryCodeService _codes;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<AccountRecoveryService> _log;

    public AccountRecoveryService(ApplicationDbContext db, CacheHelper cache, IRedisRateLimiter limiter,
        IEmailService email, IPasswordHasher hasher, IRecoveryCodeService codes, IAuditLogger audit,
        IConfiguration config, ILogger<AccountRecoveryService> log)
    {
        _db = db; _cache = cache; _limiter = limiter; _email = email; _hasher = hasher; _codes = codes;
        _audit = audit; _config = config; _log = log;
    }

    private static string TokenKey(string token) =>
        "acct:recovery:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Norm(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private string FrontendBase()
    {
        var b = _config["App:FrontendBaseUrl"];
        if (string.IsNullOrWhiteSpace(b)) b = _config["Cors:AllowedOrigins:0"];
        if (string.IsNullOrWhiteSpace(b)) b = "http://localhost:5173";
        return b.TrimEnd('/');
    }

    public async Task StartAsync(string email, string? ip, CancellationToken ct = default)
    {
        var e = Norm(email);
        if (e.Length == 0 || e.Length > 256) return;

        // Throttle silently so the response is identical whether or not we sent anything.
        var byIp = await _limiter.CheckAsync("recovery-ip", ip ?? "unknown", 10, TimeSpan.FromHours(1), ct);
        var byEmail = await _limiter.CheckAsync("recovery-email", e, 3, TimeSpan.FromHours(1), ct);
        if (!byIp.Allowed || !byEmail.Allowed) return;

        var user = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Email == e && u.IsActive).Select(u => new { u.Id, u.Email, u.DisplayName })
            .FirstOrDefaultAsync(ct);
        if (user is null) return;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        try
        {
            await _cache.Db.StringSetAsync(TokenKey(token), user.Id.ToString(), TokenTtl);
            var link = $"{FrontendBase()}/reset-password?token={Uri.EscapeDataString(token)}";
            var body = $"Hi {user.DisplayName},\n\nWe received a request to reset your Pawzaroo password. " +
                       $"Use the link below within {(int)TokenTtl.TotalMinutes} minutes:\n\n{link}\n\n" +
                       "If you didn't ask for this you can ignore this email; your password won't change.";
            await _email.SendAsync(user.Email, "Reset your Pawzaroo password", body, user.Id, ct);
            await _audit.LogAsync("AccountRecoveryRequested", "User", user.Id.ToString(), "Security", ct: ct);
        }
        catch (Exception ex)
        {
            // Never surface delivery/Redis problems: that would reveal the account exists.
            _log.LogWarning(ex, "Account recovery start failed for user {UserId}", user.Id);
        }
    }

    public async Task CompleteAsync(RecoveryCompleteInput input, string? ip, string? userAgent, CancellationToken ct = default)
    {
        ValidatePassword(input.NewPassword);
        var denied = new ForbiddenException("Invalid or expired recovery request.");

        Guid userId;
        string method;
        if (!string.IsNullOrWhiteSpace(input.Token))
        {
            var raw = await _cache.Db.StringGetDeleteAsync(TokenKey(input.Token.Trim()));   // single use
            if (raw.IsNullOrEmpty || !Guid.TryParse((string)raw!, out userId)) throw denied;
            method = "email_link";
        }
        else if (!string.IsNullOrWhiteSpace(input.RecoveryCode) && !string.IsNullOrWhiteSpace(input.Email))
        {
            var e = Norm(input.Email);
            var lim = await _limiter.CheckAsync("recovery-code", e, 5, TimeSpan.FromMinutes(15), ct);
            if (!lim.Allowed) throw denied;
            var uid = await _db.Set<User>().AsNoTracking().Where(u => u.Email == e && u.IsActive)
                .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            if (uid is null || !await _codes.ConsumeAsync(uid.Value, input.RecoveryCode, ct)) throw denied;
            userId = uid.Value;
            method = "recovery_code";
        }
        else throw denied;

        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct) ?? throw denied;
        user.PasswordHash = _hasher.Hash(input.NewPassword);

        var now = DateTime.UtcNow;
        var tokens = await _db.Set<RefreshToken>()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now).ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAt = now;

        _db.Set<ConsentRecord>().Add(new ConsentRecord
        {
            UserId = userId, ConsentType = "account_recovery", Version = method, Granted = true,
            IpAddress = ip, UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("AccountRecoveryCompleted", "User", userId.ToString(), "Security",
            newValues: new { method, sessionsRevoked = tokens.Count }, ct: ct);
    }

    private static void ValidatePassword(string? p)
    {
        var errs = new List<string>();
        if (string.IsNullOrEmpty(p) || p.Length < 8) errs.Add("Password must be at least 8 characters.");
        else if (p.Length > 128) errs.Add("Password must be at most 128 characters.");
        if (p is not null)
        {
            if (!Regex.IsMatch(p, "[A-Z]")) errs.Add("Password must contain at least one uppercase letter.");
            if (!Regex.IsMatch(p, "[a-z]")) errs.Add("Password must contain at least one lowercase letter.");
            if (!Regex.IsMatch(p, "[0-9]")) errs.Add("Password must contain at least one digit.");
        }
        if (errs.Count > 0)
            throw new ValidationException(new Dictionary<string, string[]> { ["newPassword"] = errs.ToArray() });
    }
}
