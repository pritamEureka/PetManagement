using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.SecurityPlus;

/// <summary>
/// Minimal in-box WebAuthn verification (no external dependency): CBOR/COSE parsing for
/// ES256 (P-256) credentials, clientDataJSON / authenticatorData checks and assertion signatures.
/// Attestation statements are not verified (equivalent to attestation conveyance "none").
/// </summary>
internal static class WebAuthnCrypto
{
    public const byte FlagUp = 0x01, FlagUv = 0x04, FlagAt = 0x40;

    public static AppException Fail(string msg = "Passkey verification failed.") => new("passkey_invalid", msg);

    public static string B64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromB64Url(string s)
    {
        try
        {
            var t = s.Replace('-', '+').Replace('_', '/');
            t = t.PadRight(t.Length + (4 - t.Length % 4) % 4, '=');
            return Convert.FromBase64String(t);
        }
        catch (FormatException) { throw Fail(); }
    }

    public record ClientData(string Type, string Challenge, string Origin);

    public static ClientData ParseClientData(byte[] json, string expectedType, IReadOnlyCollection<string> origins)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var type = r.GetProperty("type").GetString() ?? "";
            var challenge = r.GetProperty("challenge").GetString() ?? "";
            var origin = r.GetProperty("origin").GetString() ?? "";
            if (type != expectedType) throw Fail();
            if (!origins.Contains(origin, StringComparer.OrdinalIgnoreCase)) throw Fail("Unexpected origin.");
            return new ClientData(type, challenge, origin);
        }
        catch (AppException) { throw; }
        catch (Exception) { throw Fail(); }
    }

    public record AuthData(byte[] RpIdHash, byte Flags, uint SignCount, byte[]? CredentialId, byte[]? CoseKey);

    public static AuthData ParseAuthData(byte[] d, bool expectCredential)
    {
        if (d.Length < 37) throw Fail();
        var rpIdHash = d[..32];
        var flags = d[32];
        var counter = ((uint)d[33] << 24) | ((uint)d[34] << 16) | ((uint)d[35] << 8) | d[36];
        byte[]? credId = null, cose = null;
        if (expectCredential)
        {
            if ((flags & FlagAt) == 0 || d.Length < 55) throw Fail();
            var pos = 37 + 16; // skip AAGUID
            var len = (d[pos] << 8) | d[pos + 1];
            pos += 2;
            if (len == 0 || len > 1023 || pos + len > d.Length) throw Fail();
            credId = d[pos..(pos + len)];
            pos += len;
            var start = pos;
            Cbor.Read(d, ref pos);              // COSE key; advances past it
            cose = d[start..pos];
        }
        return new AuthData(rpIdHash, flags, counter, credId, cose);
    }

    public static void CheckRpAndFlags(AuthData a, string rpId)
    {
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(rpId));
        if (!CryptographicOperations.FixedTimeEquals(expected, a.RpIdHash)) throw Fail("RP mismatch.");
        if ((a.Flags & FlagUp) == 0) throw Fail("User presence required.");
        if ((a.Flags & FlagUv) == 0) throw Fail("User verification required.");
    }

    /// <summary>Converts a COSE_Key (EC2 / ES256 / P-256) to DER SubjectPublicKeyInfo.</summary>
    public static byte[] CoseToSpki(byte[] cose)
    {
        int p = 0;
        if (Cbor.Read(cose, ref p) is not Dictionary<object, object> m) throw Fail();
        long Get(long k) => m.TryGetValue(k, out var v) && v is long l ? l : throw Fail("Unsupported key.");
        if (Get(1) != 2 || Get(3) != -7 || Get(-1) != 1) throw Fail("Only ES256 (P-256) passkeys are supported.");
        if (!m.TryGetValue(-2L, out var xo) || xo is not byte[] x || x.Length != 32) throw Fail();
        if (!m.TryGetValue(-3L, out var yo) || yo is not byte[] y || y.Length != 32) throw Fail();
        using var ec = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y }
        });
        return ec.ExportSubjectPublicKeyInfo();
    }

    public static bool VerifyAssertion(byte[] spki, byte[] authData, byte[] clientDataJson, byte[] signature)
    {
        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(spki, out _);
            var signed = authData.Concat(SHA256.HashData(clientDataJson)).ToArray();
            return ec.VerifyData(signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException) { return false; }
    }

    /// <summary>Tiny CBOR reader: ints -> long, byte strings -> byte[], text -> string, arrays -> List, maps -> Dictionary.</summary>
    internal static class Cbor
    {
        public static object? Read(byte[] d, ref int pos, int depth = 0)
        {
            if (depth > 16 || pos >= d.Length) throw Fail();
            var ib = d[pos++];
            int major = ib >> 5, ai = ib & 0x1F;
            if (major == 7)
            {
                switch (ai)
                {
                    case 20: return false;
                    case 21: return true;
                    case 22: case 23: return null;
                    case 25: Skip(d, ref pos, 2); return 0.0;
                    case 26: Skip(d, ref pos, 4); return 0.0;
                    case 27: Skip(d, ref pos, 8); return 0.0;
                    default: throw Fail();
                }
            }
            var n = ReadArg(d, ref pos, ai);
            switch (major)
            {
                case 0:
                    if (n > long.MaxValue) throw Fail();
                    return (long)n;
                case 1:
                    if (n > long.MaxValue) throw Fail();
                    return -1L - (long)n;
                case 2:
                case 3:
                    if (n > (ulong)(d.Length - pos)) throw Fail();
                    var bytes = d[pos..(pos + (int)n)];
                    pos += (int)n;
                    return major == 2 ? bytes : Encoding.UTF8.GetString(bytes);
                case 4:
                    if (n > 1024) throw Fail();
                    var list = new List<object?>();
                    for (ulong i = 0; i < n; i++) list.Add(Read(d, ref pos, depth + 1));
                    return list;
                case 5:
                    if (n > 1024) throw Fail();
                    var map = new Dictionary<object, object>();
                    for (ulong i = 0; i < n; i++)
                    {
                        var k = Read(d, ref pos, depth + 1) ?? throw Fail();
                        var v = Read(d, ref pos, depth + 1);
                        map[k] = v!;
                    }
                    return map;
                case 6: return Read(d, ref pos, depth + 1); // tag: ignore
                default: throw Fail();
            }
        }

        private static void Skip(byte[] d, ref int pos, int n)
        {
            if (pos + n > d.Length) throw Fail();
            pos += n;
        }

        private static ulong ReadArg(byte[] d, ref int pos, int ai)
        {
            if (ai < 24) return (ulong)ai;
            var size = ai switch { 24 => 1, 25 => 2, 26 => 4, 27 => 8, _ => throw Fail() };
            if (pos + size > d.Length) throw Fail();
            ulong v = 0;
            for (var i = 0; i < size; i++) v = (v << 8) | d[pos++];
            return v;
        }
    }
}
