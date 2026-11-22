using System.Security.Cryptography;
using System.Text;

namespace Pawzaroo.Infrastructure.Storage;

/// <summary>HMAC-SHA256 + expiry signing for proxied media links (GET /api/media/signed).</summary>
public interface IMediaUrlSigner
{
    string Sign(string key, TimeSpan ttl);
    bool Verify(string key, long expUnix, string sig);
}

public class MediaUrlSigner : IMediaUrlSigner
{
    private readonly byte[] _secret;
    private readonly Func<DateTimeOffset> _now;

    public MediaUrlSigner(byte[] secret, Func<DateTimeOffset>? now = null)
    {
        _secret = secret;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public static string ComputeSignature(byte[] secret, string key, long expUnix)
    {
        using var h = new HMACSHA256(secret);
        var mac = h.ComputeHash(Encoding.UTF8.GetBytes($"{key}\n{expUnix}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Returns a relative URL: /api/media/signed?key=..&amp;exp=..&amp;sig=..</summary>
    public string Sign(string key, TimeSpan ttl)
    {
        var exp = _now().Add(ttl).ToUnixTimeSeconds();
        return $"/api/media/signed?key={Uri.EscapeDataString(key)}&exp={exp}&sig={ComputeSignature(_secret, key, exp)}";
    }

    public bool Verify(string key, long expUnix, string sig)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(sig)) return false;
        if (key.Contains("..") || key.StartsWith('/')) return false;
        if (expUnix < _now().ToUnixTimeSeconds()) return false;
        var expected = Encoding.ASCII.GetBytes(ComputeSignature(_secret, key, expUnix));
        var given = Encoding.ASCII.GetBytes(sig);
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}
