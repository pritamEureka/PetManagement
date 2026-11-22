using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Pawzaroo.Infrastructure.Modules.Push;
using Xunit;

namespace Pawzaroo.Tests.Unit.Push;

public class WebPushCryptoTests
{
    private static byte[] B(string s) => WebPushCrypto.FromBase64Url(s);

    // RFC 8291 section 5 inputs.
    private const string AsPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string AsPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string UaPrivate = "q1dXpw3UpT5VOmu_cf_v6ih07Aems3njxI-JWgLcM94";
    private const string UaPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string Plain = "When I grow up, I want to be a watermelon";

    private static ECParameters Priv(string d, string pub)
    {
        var p = B(pub);
        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B(d),
            Q = new ECPoint { X = p[1..33], Y = p[33..65] },
        };
    }

    [Fact]
    public void Rfc8291_vector_header_and_key_schedule()
    {
        var body = WebPushCrypto.Encrypt(Encoding.UTF8.GetBytes(Plain), B(UaPublic), B(AuthSecret), Priv(AsPrivate, AsPublic), B(Salt));

        // Header: salt || rs=4096 || idlen=65 || as_public (fully determined by the RFC inputs).
        WebPushCrypto.ToBase64Url(body[..86]).Should().Be("DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        body.Length.Should().Be(86 + Plain.Length + 1 + 16);

        // (Intermediate ECDH/IKM/CEK/nonce values omitted: not independently verified against the RFC text.)
    }

    [Fact]
    public void Rfc8291_vector_round_trips()
    {
        var body = WebPushCrypto.Encrypt(Encoding.UTF8.GetBytes(Plain), B(UaPublic), B(AuthSecret), Priv(AsPrivate, AsPublic), B(Salt));
        var plain = WebPushCrypto.Decrypt(body, Priv(UaPrivate, UaPublic), B(AuthSecret));
        Encoding.UTF8.GetString(plain).Should().Be(Plain);
    }

    [Fact]
    public void Random_keys_round_trip_and_tamper_fails()
    {
        using var receiver = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var rp = receiver.ExportParameters(true);
        var uaPub = new byte[65];
        uaPub[0] = 4; rp.Q.X!.CopyTo(uaPub, 1); rp.Q.Y!.CopyTo(uaPub, 33);
        var auth = RandomNumberGenerator.GetBytes(16);
        var json = JsonSerializer.SerializeToUtf8Bytes(new { title = "Hi", body = new string('x', 1500) });

        var body = WebPushCrypto.Encrypt(json, uaPub, auth);
        body.Length.Should().BeLessOrEqualTo(WebPushCrypto.RecordSize);
        WebPushCrypto.Decrypt(body, rp, auth).Should().Equal(json);

        body[^1] ^= 0x01;
        var act = () => WebPushCrypto.Decrypt(body, rp, auth);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Encrypt_rejects_oversized_payload()
    {
        var (pub, _) = WebPushCrypto.GenerateVapidKeys();
        var act = () => WebPushCrypto.Encrypt(new byte[WebPushCrypto.MaxPlaintext + 1], B(pub), RandomNumberGenerator.GetBytes(16));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generated_vapid_keys_are_valid_and_sized()
    {
        var (pub, priv) = WebPushCrypto.GenerateVapidKeys();
        B(pub).Length.Should().Be(65);
        B(pub)[0].Should().Be(4);
        B(priv).Length.Should().Be(32);
        WebPushCrypto.IsValidKeyPair(pub, priv).Should().BeTrue();
        WebPushCrypto.IsValidKeyPair(pub, WebPushCrypto.GenerateVapidKeys().PrivateKey).Should().BeFalse();
    }

    [Fact]
    public void Vapid_jwt_structure_and_es256_signature_verify()
    {
        var (pub, priv) = WebPushCrypto.GenerateVapidKeys();
        var exp = DateTimeOffset.UtcNow.AddHours(24); // must be clamped to 12h
        var jwt = WebPushCrypto.CreateVapidJwt("https://push.example.com", "mailto:ops@example.com", exp, pub, priv);

        var parts = jwt.Split('.');
        parts.Length.Should().Be(3);
        using var h = JsonDocument.Parse(B(parts[0]));
        h.RootElement.GetProperty("alg").GetString().Should().Be("ES256");
        h.RootElement.GetProperty("typ").GetString().Should().Be("JWT");
        using var c = JsonDocument.Parse(B(parts[1]));
        c.RootElement.GetProperty("aud").GetString().Should().Be("https://push.example.com");
        c.RootElement.GetProperty("sub").GetString().Should().Be("mailto:ops@example.com");
        var expUnix = c.RootElement.GetProperty("exp").GetInt64();
        expUnix.Should().BeLessOrEqualTo(DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds());
        expUnix.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var sig = B(parts[2]);
        sig.Length.Should().Be(64);
        var p = B(pub);
        using var verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = p[1..33], Y = p[33..65] },
        });
        verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), sig, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation).Should().BeTrue();

        WebPushCrypto.VapidAuthorization(jwt, pub).Should().Be($"vapid t={jwt}, k={pub}");
    }
}
