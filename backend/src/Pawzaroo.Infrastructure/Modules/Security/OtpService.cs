using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// 6-digit OTP with rate-limited verification and constant-time comparison.
///
///  * Issuing also rate-limits via Redis to keep SMS/email costs bounded.
///  * Codes are stored as SHA-256 hash (with the user's id as salt prefix);
///    plaintext is never persisted.
///  * Max 5 verify attempts per code; after that the row is consumed.
/// </summary>
public class OtpService : IOtpService
{
    private readonly ApplicationDbContext _db;
    private readonly IOtpDeliveryService _delivery;
    private readonly IRedisRateLimiter _rate;
    private readonly ILogger<OtpService> _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 5;

    public OtpService(ApplicationDbContext db, IOtpDeliveryService delivery,
        IRedisRateLimiter rate, ILogger<OtpService> logger)
    {
        _db = db;
        _delivery = delivery;
        _rate = rate;
        _logger = logger;
    }

    public async Task IssueAsync(Guid userId, OtpPurpose purpose, string destination, CancellationToken ct = default)
    {
        var decision = await _rate.CheckAsync("otp.issue", $"{userId}:{purpose}", permitLimit: 5, window: TimeSpan.FromMinutes(15), ct);
        if (!decision.Allowed) throw new Pawzaroo.Shared.Exceptions.ConflictException("Too many code requests; try later.");

        var code = GenerateCode();
        var row = new OtpCode
        {
            UserId = userId,
            Purpose = purpose,
            CodeHash = Hash(userId, code),
            ExpiresAt = DateTime.UtcNow.Add(Ttl),
            Destination = destination
        };
        _db.OtpCodes.Add(row);

        // Mark any open codes of the same purpose as consumed so the latest wins.
        var stale = await _db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.Consumed && o.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        foreach (var s in stale) s.Consumed = true;

        await _db.SaveChangesAsync(ct);

        var body = $"Your Pawzaroo verification code is {code}. It expires in {(int)Ttl.TotalMinutes} minutes.";
        try
        {
            if (destination.Contains('@'))
                await _delivery.SendEmailAsync(destination, "Your Pawzaroo verification code", body, ct);
            else
                await _delivery.SendSmsAsync(destination, body, ct);
        }
        catch (Exception ex)
        {
            // We swallow because the code is already issued — we don't want a delivery
            // failure to make `IssueAsync` look like it didn't happen.
            _logger.LogWarning(ex, "OTP delivery failed for {Destination}", destination);
        }
    }

    public async Task<bool> VerifyAsync(Guid userId, OtpPurpose purpose, string code, CancellationToken ct = default)
    {
        var row = await _db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.Consumed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (row is null) return false;

        row.Attempts++;
        var expected = Hash(userId, code);

        var ok = ConstantTimeEquals(expected, row.CodeHash);
        if (ok || row.Attempts >= MaxAttempts) row.Consumed = true;
        await _db.SaveChangesAsync(ct);
        return ok;
    }

    private static string GenerateCode()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        var n = BitConverter.ToUInt32(buf) % 1_000_000u;
        return n.ToString("D6");
    }

    private static string Hash(Guid userId, string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}|{code}"));
        return Convert.ToHexString(bytes);
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

/// <summary>
/// Log-only OTP delivery for dev. Wire a real adapter (SES, SendGrid, Twilio)
/// behind <see cref="IOtpDeliveryService"/> in production.
/// </summary>
public class ConsoleOtpDelivery : IOtpDeliveryService
{
    private readonly ILogger<ConsoleOtpDelivery> _logger;
    public ConsoleOtpDelivery(ILogger<ConsoleOtpDelivery> logger) => _logger = logger;
    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[otp/email] to={To} subject={Subject} body={Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
    public Task SendSmsAsync(string toPhone, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[otp/sms] to={To} body={Body}", toPhone, body);
        return Task.CompletedTask;
    }
}
