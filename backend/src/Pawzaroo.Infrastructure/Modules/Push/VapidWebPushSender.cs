using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pawzaroo.Application.Modules.Push;

namespace Pawzaroo.Infrastructure.Modules.Push;

/// <summary>
/// Web Push primitives implemented with in-box crypto only:
/// RFC 8291 (aes128gcm message encryption) and RFC 8292 (VAPID, ES256 JWT).
/// </summary>
public static class WebPushCrypto
{
    public const int RecordSize = 4096;
    private const int HeaderFixed = 16 + 4 + 1;           // salt + rs + idlen
    private const int TagSize = 16;
    /// <summary>Max plaintext bytes that fit one 4096-byte record: 4096 - header(86) - delimiter(1) - tag(16).</summary>
    public const int MaxPlaintext = RecordSize - (HeaderFixed + 65) - 1 - TagSize;

    // ---- base64url -------------------------------------------------------------------------

    public static string ToBase64Url(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string s)
    {
        var t = s.Trim().Replace('-', '+').Replace('_', '/');
        t = t.PadRight(t.Length + (4 - t.Length % 4) % 4, '=');
        return Convert.FromBase64String(t);
    }

    // ---- VAPID keys ------------------------------------------------------------------------

    /// <summary>
    /// Generates a VAPID key pair for the Push:VapidPublicKey / Push:VapidPrivateKey settings.
    /// Public = base64url of the 65-byte uncompressed P-256 point (what the browser's
    /// applicationServerKey expects); private = base64url of the 32-byte scalar.
    /// Example (dotnet script / test / one-off tool): <c>var (pub, priv) = WebPushCrypto.GenerateVapidKeys();</c>
    /// Keep the private key in a secret store (env var <c>Push__VapidPrivateKey</c>); never expose it.
    /// </summary>
    public static (string PublicKey, string PrivateKey) GenerateVapidKeys()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(true);
        return (ToBase64Url(Uncompressed(p.Q)), ToBase64Url(p.D!));
    }

    private static byte[] Uncompressed(ECPoint q)
    {
        var b = new byte[65];
        b[0] = 0x04;
        q.X!.CopyTo(b, 1 + 32 - q.X.Length);
        q.Y!.CopyTo(b, 33 + 32 - q.Y.Length);
        return b;
    }

    private static ECPoint ParsePoint(byte[] pub)
    {
        if (pub.Length != 65 || pub[0] != 0x04) throw new ArgumentException("Expected a 65-byte uncompressed P-256 public key.");
        return new ECPoint { X = pub[1..33], Y = pub[33..65] };
    }

    public static bool IsValidKeyPair(string publicKey, string privateKey)
    {
        try
        {
            var pub = FromBase64Url(publicKey);
            var d = FromBase64Url(privateKey);
            if (d.Length != 32) return false;
            using var ec = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = d, Q = ParsePoint(pub) });
            return true;
        }
        catch { return false; }
    }

    // ---- VAPID JWT (RFC 8292) --------------------------------------------------------------

    /// <summary>ES256 JWT: header {typ:JWT,alg:ES256}, claims {aud, exp, sub}. Signature is raw r||s (P1363).</summary>
    public static string CreateVapidJwt(string audience, string subject, DateTimeOffset expires, string publicKey, string privateKey)
    {
        var maxExp = DateTimeOffset.UtcNow.AddHours(12);
        if (expires > maxExp) expires = maxExp;
        var header = ToBase64Url(Encoding.UTF8.GetBytes("{\"typ\":\"JWT\",\"alg\":\"ES256\"}"));
        var claims = ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["exp"] = expires.ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));
        var signingInput = header + "." + claims;
        using var ec = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(privateKey),
            Q = ParsePoint(FromBase64Url(publicKey)),
        });
        var sig = ec.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return signingInput + "." + ToBase64Url(sig);
    }

    /// <summary>Value for the Authorization header: <c>vapid t=&lt;jwt&gt;, k=&lt;public key&gt;</c>.</summary>
    public static string VapidAuthorization(string jwt, string publicKey) => $"vapid t={jwt}, k={publicKey}";

    // ---- RFC 8291 encryption ---------------------------------------------------------------

    public record DerivedKeys(byte[] EcdhSecret, byte[] Ikm, byte[] Cek, byte[] Nonce);

    /// <summary>
    /// Key schedule shared by encrypt and decrypt. <paramref name="uaPublic"/> is the receiver (browser) public key,
    /// <paramref name="asPublic"/> the sender's ephemeral public key; <paramref name="ecdhSecret"/> the raw ECDH output.
    /// </summary>
    public static DerivedKeys DeriveKeys(byte[] ecdhSecret, byte[] authSecret, byte[] uaPublic, byte[] asPublic, byte[] salt)
    {
        var prkKey = HKDF.Extract(HashAlgorithmName.SHA256, ecdhSecret, authSecret);
        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), uaPublic, asPublic);
        var ikm = HKDF.Expand(HashAlgorithmName.SHA256, prkKey, 32, keyInfo);
        var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
        var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        return new DerivedKeys(ecdhSecret, ikm, cek, nonce);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> into a complete aes128gcm request body (header + one record).
    /// <paramref name="senderPrivate"/>/<paramref name="salt"/> are for deterministic tests; leave null in production.
    /// </summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] uaPublic, byte[] authSecret, ECParameters? senderPrivate = null, byte[]? salt = null)
    {
        if (plaintext.Length > MaxPlaintext) throw new ArgumentException($"Payload exceeds {MaxPlaintext} bytes.", nameof(plaintext));
        salt ??= RandomNumberGenerator.GetBytes(16);

        using var sender = senderPrivate is { } sp
            ? ECDiffieHellman.Create(sp)
            : ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var asPublic = Uncompressed(sender.ExportParameters(false).Q);

        using var receiver = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = ParsePoint(uaPublic) });
        var secret = sender.DeriveRawSecretAgreement(receiver.PublicKey);
        var k = DeriveKeys(secret, authSecret, uaPublic, asPublic, salt);

        var padded = new byte[plaintext.Length + 1];
        plaintext.CopyTo(padded, 0);
        padded[^1] = 0x02; // last-record delimiter, no extra padding

        var cipher = new byte[padded.Length];
        var tag = new byte[TagSize];
        using (var gcm = new AesGcm(k.Cek, TagSize))
            gcm.Encrypt(k.Nonce, padded, cipher, tag);

        var header = new byte[HeaderFixed + asPublic.Length];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16, 4), RecordSize);
        header[20] = (byte)asPublic.Length;
        asPublic.CopyTo(header, HeaderFixed);
        return Concat(header, cipher, tag);
    }

    /// <summary>Receiver-side decryption (used by tests and handy for diagnostics).</summary>
    public static byte[] Decrypt(byte[] body, ECParameters receiverPrivate, byte[] authSecret)
    {
        var salt = body[..16];
        var idLen = body[20];
        var asPublic = body[HeaderFixed..(HeaderFixed + idLen)];
        var record = body[(HeaderFixed + idLen)..];
        var uaPublic = Uncompressed(receiverPrivate.Q);

        using var receiver = ECDiffieHellman.Create(receiverPrivate);
        using var sender = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = ParsePoint(asPublic) });
        var secret = receiver.DeriveRawSecretAgreement(sender.PublicKey);
        var k = DeriveKeys(secret, authSecret, uaPublic, asPublic, salt);

        var cipher = record[..^TagSize];
        var tag = record[^TagSize..];
        var plain = new byte[cipher.Length];
        using (var gcm = new AesGcm(k.Cek, TagSize))
            gcm.Decrypt(k.Nonce, cipher, tag, plain);
        var end = plain.Length;
        while (end > 0 && plain[end - 1] == 0) end--;      // strip zero padding
        if (end == 0 || plain[end - 1] != 0x02) throw new CryptographicException("Missing record delimiter.");
        return plain[..(end - 1)];
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var o = new byte[parts.Sum(p => p.Length)];
        var i = 0;
        foreach (var p in parts) { p.CopyTo(o, i); i += p.Length; }
        return o;
    }
}

/// <summary>
/// Real Web Push transport (RFC 8030 + 8291 + 8292). Selected at runtime by
/// <see cref="WebPushSenderFactory"/> only when Push:VapidPublicKey and Push:VapidPrivateKey are valid.
/// HTTP 404/410 map to <see cref="WebPushResult.Gone"/> so <c>PushDispatcher</c> deletes the subscription.
/// </summary>
public class VapidWebPushSender : IWebPushSender
{
    public const string HttpClientName = "webpush";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<VapidWebPushSender> _log;
    private readonly string _pub;
    private readonly string _priv;
    private readonly string _subject;
    private readonly int _ttl;

    public VapidWebPushSender(IHttpClientFactory http, IConfiguration cfg, ILogger<VapidWebPushSender> log)
    {
        _http = http;
        _log = log;
        _pub = cfg["Push:VapidPublicKey"] ?? "";
        _priv = cfg["Push:VapidPrivateKey"] ?? "";
        _subject = string.IsNullOrWhiteSpace(cfg["Push:Subject"]) ? "mailto:admin@pawzaroo.local" : cfg["Push:Subject"]!;
        _ttl = int.TryParse(cfg["Push:TtlSeconds"], out var t) && t > 0 ? t : 86400;
    }

    internal static string UrgencyFor(string? category) => category switch
    {
        NotificationCategories.Messages or NotificationCategories.Appointments or NotificationCategories.LostFound => "high",
        NotificationCategories.Social or NotificationCategories.Community => "low",
        _ => "normal",
    };

    public async Task<WebPushResult> SendAsync(string endpoint, string p256dh, string auth, PushMessage message, CancellationToken ct = default)
    {
        try
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return WebPushResult.Failed;
            var audience = uri.GetLeftPart(UriPartial.Authority);

            var json = JsonSerializer.SerializeToUtf8Bytes(new { title = message.Title, body = message.Body, url = message.Url, category = message.Category });
            if (json.Length > WebPushCrypto.MaxPlaintext)
            {
                var body = message.Body.Length > 200 ? message.Body[..200] : message.Body;
                json = JsonSerializer.SerializeToUtf8Bytes(new { title = message.Title, body, url = message.Url, category = message.Category });
            }
            var payload = WebPushCrypto.Encrypt(json, WebPushCrypto.FromBase64Url(p256dh), WebPushCrypto.FromBase64Url(auth));

            var jwt = WebPushCrypto.CreateVapidJwt(audience, _subject, DateTimeOffset.UtcNow.AddHours(11), _pub, _priv);

            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            req.Headers.TryAddWithoutValidation("Authorization", WebPushCrypto.VapidAuthorization(jwt, _pub));
            req.Headers.TryAddWithoutValidation("TTL", _ttl.ToString());
            req.Headers.TryAddWithoutValidation("Urgency", UrgencyFor(message.Category));
            req.Content = new ByteArrayContent(payload);
            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            req.Content.Headers.ContentEncoding.Add("aes128gcm");

            var client = _http.CreateClient(HttpClientName);
            using var res = await client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode) return WebPushResult.Sent;
            if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return WebPushResult.Gone;
            _log.LogWarning("[web-push] {Host} responded {Status}", uri.Host, (int)res.StatusCode);
            return WebPushResult.Failed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[web-push] send failed");
            return WebPushResult.Failed;
        }
    }
}

public static class WebPushSenderFactory
{
    /// <summary>Real sender when VAPID keys are configured and valid; otherwise the logging sender.</summary>
    public static IWebPushSender Create(IServiceProvider sp)
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var pub = cfg["Push:VapidPublicKey"];
        var priv = cfg["Push:VapidPrivateKey"];
        var logs = sp.GetRequiredService<ILoggerFactory>();
        if (!string.IsNullOrWhiteSpace(pub) && !string.IsNullOrWhiteSpace(priv))
        {
            if (WebPushCrypto.IsValidKeyPair(pub, priv))
                return new VapidWebPushSender(sp.GetRequiredService<IHttpClientFactory>(), cfg, logs.CreateLogger<VapidWebPushSender>());
            logs.CreateLogger("WebPush").LogWarning("Push VAPID keys are set but invalid; falling back to log-only sender.");
        }
        return new LoggingWebPushSender(logs.CreateLogger<LoggingWebPushSender>());
    }
}
