using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// RFC 6238 TOTP (30s window, SHA-1, 6 digits) — Google Authenticator / 1Password
/// compatible. Recovery codes are bcrypt-hashed and stored as a JSON array.
/// Secrets are AES-encrypted at rest with the JWT signing key (deliberately
/// reusing an already-secret value to avoid yet another KMS dependency in
/// dev; production should swap in a dedicated key wrapper).
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPasswordHasher _hasher;
    private readonly Pawzaroo.Infrastructure.Identity.JwtSettings _jwtSettings;

    private const int SecretBytes = 20;     // 160-bit secret per RFC 6238
    private const int RecoveryCodes = 8;
    private const int Period = 30;
    private const int Digits = 6;

    public TwoFactorService(ApplicationDbContext db, ICurrentUserService current,
        IPasswordHasher hasher, Microsoft.Extensions.Options.IOptions<Pawzaroo.Infrastructure.Identity.JwtSettings> jwt)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
        _jwtSettings = jwt.Value;
    }

    public async Task<TwoFactorSetupDto> BeginSetupAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var email = _current.Email ?? "user";

        var secret = RandomNumberGenerator.GetBytes(SecretBytes);
        var base32 = Base32Encode(secret);

        var codes = Enumerable.Range(0, RecoveryCodes)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(5))).ToList();
        var hashed = codes.Select(c => _hasher.Hash(c)).ToList();

        var tf = await _db.TwoFactorSettings.FirstOrDefaultAsync(t => t.UserId == uid, ct);
        if (tf is null)
        {
            tf = new TwoFactorSettings { UserId = uid };
            _db.TwoFactorSettings.Add(tf);
        }
        tf.EncryptedSecret    = EncryptSeed(secret);
        tf.RecoveryCodesHash  = System.Text.Json.JsonSerializer.Serialize(hashed);
        tf.IsEnabled          = false;
        await _db.SaveChangesAsync(ct);

        var uri = $"otpauth://totp/Pawzaroo:{Uri.EscapeDataString(email)}" +
                  $"?secret={base32}&issuer=Pawzaroo&algorithm=SHA1&digits={Digits}&period={Period}";
        return new TwoFactorSetupDto(base32, uri, codes);
    }

    public async Task<bool> ConfirmEnableAsync(string code, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var tf = await _db.TwoFactorSettings.FirstOrDefaultAsync(t => t.UserId == uid, ct)
                 ?? throw new ConflictException("Call BeginSetup first.");
        if (tf.EncryptedSecret is null) throw new ConflictException("No setup in progress.");

        var secret = DecryptSeed(tf.EncryptedSecret);
        if (!VerifyTotp(secret, code)) return false;

        tf.IsEnabled = true;
        tf.EnabledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DisableAsync(string code, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var tf = await _db.TwoFactorSettings.FirstOrDefaultAsync(t => t.UserId == uid, ct);
        if (tf is null || !tf.IsEnabled) return true;

        var secret = DecryptSeed(tf.EncryptedSecret!);
        if (!VerifyTotp(secret, code)) return false;

        tf.IsEnabled = false;
        tf.EncryptedSecret = null;
        tf.RecoveryCodesHash = null;
        tf.EnabledAt = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var tf = await _db.TwoFactorSettings.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsEnabled, ct);
        if (tf is null) return true;        // 2FA not enabled => trivially pass

        // Try recovery codes first (8-char hex, hashed).
        if (!string.IsNullOrWhiteSpace(tf.RecoveryCodesHash))
        {
            var codes = System.Text.Json.JsonSerializer.Deserialize<string[]>(tf.RecoveryCodesHash) ?? Array.Empty<string>();
            if (codes.Any(h => _hasher.Verify(code, h)))
            {
                // Burn the matched code so it can't be reused.
                var remaining = codes.Where(h => !_hasher.Verify(code, h)).ToArray();
                var tracked = await _db.TwoFactorSettings.FirstAsync(t => t.UserId == userId, ct);
                tracked.RecoveryCodesHash = System.Text.Json.JsonSerializer.Serialize(remaining);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }

        var secret = DecryptSeed(tf.EncryptedSecret!);
        return VerifyTotp(secret, code);
    }

    public async Task<bool> IsEnabledAsync(Guid userId, CancellationToken ct = default)
        => await _db.TwoFactorSettings.AsNoTracking().AnyAsync(t => t.UserId == userId && t.IsEnabled, ct);

    // --- TOTP helpers ------------------------------------------------------

    private static bool VerifyTotp(byte[] secret, string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != Digits) return false;
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / Period;
        // Allow ±1 step drift.
        for (long i = -1; i <= 1; i++)
        {
            if (ComputeTotp(secret, step + i) == code) return true;
        }
        return false;
    }

    private static string ComputeTotp(byte[] secret, long step)
    {
        var bytes = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(bytes);
        var offset = hash[^1] & 0x0F;
        var bin = ((hash[offset] & 0x7F) << 24)
                | ((hash[offset + 1] & 0xFF) << 16)
                | ((hash[offset + 2] & 0xFF) << 8)
                | (hash[offset + 3] & 0xFF);
        return (bin % (int)Math.Pow(10, Digits)).ToString().PadLeft(Digits, '0');
    }

    // --- Seed encryption (AES-GCM with key derived from JWT signing key) ---

    private string EncryptSeed(byte[] secret)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey + ":2fa"));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[secret.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, secret, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    private byte[] DecryptSeed(string encoded)
    {
        var blob = Convert.FromBase64String(encoded);
        var nonce = blob[..12];
        var tag = blob[12..28];
        var cipher = blob[28..];
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey + ":2fa"));
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static string Base32Encode(byte[] data)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((int)Math.Ceiling(data.Length * 8 / 5d));
        int buffer = data[0]; int next = 1; int bitsLeft = 8;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length) { buffer = (buffer << 8) | data[next++]; bitsLeft += 8; }
                else { buffer <<= 5 - bitsLeft; bitsLeft = 5; }
            }
            var index = (buffer >> (bitsLeft - 5)) & 0x1F;
            bitsLeft -= 5;
            sb.Append(Alphabet[index]);
        }
        return sb.ToString();
    }
}
