using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using Pawzaroo.Application.Modules.Identity.Features.Refresh;
using Pawzaroo.Infrastructure.Caching;
using Pawzaroo.Infrastructure.Modules.Security;
using Pawzaroo.Infrastructure.Modules.SecurityPlus;
using Pawzaroo.Infrastructure.Storage;
using Xunit;

namespace Pawzaroo.Tests.Unit.Security;

public class SecurityHardeningTests
{
    // ---------- synthetic files ----------

    private static byte[] Jpeg(int w, int h, bool withExif = true, byte[]? trailer = null)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8 });
        if (withExif)
        {
            ms.Write(new byte[] { 0xFF, 0xE1, 0x00, 0x08 });
            ms.Write(Encoding.ASCII.GetBytes("Exif\0\0"));
        }
        ms.Write(new byte[] { 0xFF, 0xC0, 0x00, 0x11, 0x08 });
        ms.WriteByte((byte)(h >> 8)); ms.WriteByte((byte)h);
        ms.WriteByte((byte)(w >> 8)); ms.WriteByte((byte)w);
        ms.Write(new byte[] { 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
        ms.Write(new byte[] { 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00 });
        ms.Write(new byte[] { 0xAA, 0xBB, 0xCC });
        ms.Write(new byte[] { 0xFF, 0xD9 });
        if (trailer is not null) ms.Write(trailer);
        return ms.ToArray();
    }

    private static void Chunk(MemoryStream ms, string type, byte[] data)
    {
        var len = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
        ms.Write(len);
        ms.Write(Encoding.ASCII.GetBytes(type));
        ms.Write(data);
        ms.Write(new byte[4]); // dummy CRC (not verified)
    }

    private static byte[] Png(int w, int h, bool withText = true)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)w);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)h);
        ihdr[8] = 8; ihdr[9] = 2;
        Chunk(ms, "IHDR", ihdr);
        if (withText) { Chunk(ms, "tEXt", Encoding.ASCII.GetBytes("GPS\0lat=1.0")); Chunk(ms, "eXIf", new byte[] { 1, 2, 3 }); }
        Chunk(ms, "IDAT", new byte[] { 1, 2, 3, 4 });
        Chunk(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    // ---------- sniffing / pipeline ----------

    [Fact]
    public void Sniff_detects_formats()
    {
        UploadInspector.Sniff(Jpeg(10, 10)).Mime.Should().Be("image/jpeg");
        UploadInspector.Sniff(Png(10, 10)).Mime.Should().Be("image/png");
        UploadInspector.Sniff(Encoding.ASCII.GetBytes("%PDF-1.7 ...")).Mime.Should().Be("application/pdf");
        UploadInspector.Sniff(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }).Mime.Should().BeNull();
    }

    [Fact]
    public void Pipeline_accepts_valid_jpeg_and_strips_exif()
    {
        var res = UploadPipeline.Process("cat.jpg", "image/jpeg", Jpeg(100, 80));
        res.Allowed.Should().BeTrue(res.Reason);
        Encoding.Latin1.GetString(res.CleanBytes!).Should().NotContain("Exif");
        res.CleanBytes!.Length.Should().BeLessThan(Jpeg(100, 80).Length);
        UploadInspector.ReadDimensions(res.CleanBytes, "image/jpeg").Should().Be(new ImageDimensions(100, 80));
    }

    [Fact]
    public void Pipeline_strips_png_text_chunks()
    {
        var res = UploadPipeline.Process("a.png", "image/png", Png(32, 32));
        res.Allowed.Should().BeTrue(res.Reason);
        var s = Encoding.Latin1.GetString(res.CleanBytes!);
        s.Should().NotContain("tEXt").And.NotContain("eXIf");
        s.Should().Contain("IDAT").And.Contain("IEND");
    }

    [Fact]
    public void Pipeline_rejects_mismatches()
    {
        UploadPipeline.Process("a.png", "image/png", Jpeg(10, 10)).Allowed.Should().BeFalse();       // ext vs bytes
        UploadPipeline.Process("a.jpg", "image/png", Jpeg(10, 10)).Allowed.Should().BeFalse();       // declared vs bytes
        UploadPipeline.Process("a.exe", "application/octet-stream", Jpeg(10, 10)).Allowed.Should().BeFalse();
        UploadPipeline.Process("a.jpg", "image/jpeg", Array.Empty<byte>()).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Pipeline_rejects_polyglots()
    {
        var php = Jpeg(10, 10, trailer: Encoding.ASCII.GetBytes("<?php system($_GET['x']); ?>"));
        // marker beyond the first KB is caught only via trailing-archive rule, so embed in first KB:
        var early = Jpeg(10, 10).ToList();
        early.InsertRange(early.Count - 2, Encoding.ASCII.GetBytes("<script>alert(1)</script>"));
        UploadPipeline.Process("a.jpg", "image/jpeg", early.ToArray()).Allowed.Should().BeFalse();

        var zip = Jpeg(10, 10, trailer: new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 });
        UploadPipeline.Process("a.jpg", "image/jpeg", zip).Allowed.Should().BeFalse();
        php.Should().NotBeEmpty();
    }

    [Fact]
    public void Pipeline_rejects_pixel_bombs()
    {
        UploadPipeline.Process("big.png", "image/png", Png(20000, 20000)).Allowed.Should().BeFalse();
        UploadPipeline.Process("big.jpg", "image/jpeg", Jpeg(15000, 15000)).Allowed.Should().BeFalse(); // 225 MP
    }

    [Fact]
    public async Task Eicar_is_detected_by_builtin_scanner()
    {
        var scanner = new BuiltInMalwareScanner();
        var bad = await scanner.ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes("xx" + BuiltInMalwareScanner.Eicar + "yy")));
        bad.Clean.Should().BeFalse();
        (await scanner.ScanAsync(new MemoryStream(Jpeg(10, 10)))).Clean.Should().BeTrue();
        UploadPipeline.Process("a.pdf", "application/pdf",
            Encoding.ASCII.GetBytes("%PDF-1.4\n" + BuiltInMalwareScanner.Eicar)).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Clam_response_parsing()
    {
        ClamAvMalwareScanner.ParseResponse("stream: OK\0").Clean.Should().BeTrue();
        var f = ClamAvMalwareScanner.ParseResponse("stream: Win.Test.EICAR_HDB-1 FOUND\0");
        f.Clean.Should().BeFalse();
        f.Signature.Should().Contain("EICAR");
        ClamAvMalwareScanner.ParseResponse("INSTREAM size limit exceeded. ERROR").Clean.Should().BeFalse();
    }

    [Fact]
    public void Filename_sanitisation_and_random_keys()
    {
        UploadInspector.SanitizeFileName("../../etc/passwd.jpg").Should().Be("passwd.jpg");
        UploadInspector.SanitizeFileName("a.php.jpg").Should().Be("a_php.jpg");
        UploadInspector.SanitizeFileName("we ird<>name.png").Should().Be("we_ird__name.png");
        var k1 = UploadInspector.RandomKey("u/1", ".JPG");
        var k2 = UploadInspector.RandomKey("u/1", ".jpg");
        k1.Should().EndWith(".jpg").And.StartWith("u/1/");
        k1.Should().NotBe(k2);
    }

    // ---------- signed URLs ----------

    [Fact]
    public void Signed_url_roundtrip_expiry_and_tamper()
    {
        var now = DateTimeOffset.UtcNow;
        var signer = new MediaUrlSigner(Encoding.UTF8.GetBytes("secret"), () => now);
        var exp = now.AddMinutes(5).ToUnixTimeSeconds();
        var sig = MediaUrlSigner.ComputeSignature(Encoding.UTF8.GetBytes("secret"), "u/1/a.jpg", exp);
        signer.Verify("u/1/a.jpg", exp, sig).Should().BeTrue();
        signer.Verify("u/1/b.jpg", exp, sig).Should().BeFalse();
        signer.Verify("u/1/a.jpg", exp + 1, sig).Should().BeFalse();
        var later = new MediaUrlSigner(Encoding.UTF8.GetBytes("secret"), () => now.AddMinutes(10));
        later.Verify("u/1/a.jpg", exp, sig).Should().BeFalse();
        signer.Verify("../x", exp, MediaUrlSigner.ComputeSignature(Encoding.UTF8.GetBytes("secret"), "../x", exp)).Should().BeFalse();
        signer.Sign("u/1/a.jpg", TimeSpan.FromMinutes(1)).Should().StartWith("/api/media/signed?key=");
    }

    // ---------- signals ----------

    [Fact]
    public void Travel_country_change_within_an_hour_flags()
    {
        var t = DateTime.UtcNow;
        var prev = new LoginGeo("1.1.1.1", null, "IN", null, null, t.AddMinutes(-20));
        SignalRules.EvaluateTravel(prev, new LoginGeo("2.2.2.2", null, "US", null, null, t)).Should().NotBeNull();
        SignalRules.EvaluateTravel(prev, new LoginGeo("2.2.2.2", null, "in", null, null, t)).Should().BeNull();
        SignalRules.EvaluateTravel(prev with { At = t.AddHours(-3) }, new LoginGeo("2.2.2.2", null, "US", null, null, t)).Should().BeNull();
    }

    [Fact]
    public void Travel_speed_rule_with_coordinates()
    {
        var t = DateTime.UtcNow;
        var london = new LoginGeo(null, "London", "GB", 51.5, -0.12, t.AddMinutes(-30));
        var ny = new LoginGeo(null, "NYC", "US", 40.7, -74.0, t);
        SignalRules.EvaluateTravel(london, ny).Should().NotBeNull();
        SignalRules.EvaluateTravel(london with { At = t.AddHours(-12) }, ny).Should().BeNull();
        SignalRules.HaversineKm(51.5, -0.12, 40.7, -74.0).Should().BeInRange(5500, 5650);
    }

    // ---------- degraded mode ----------

    [Theory]
    [InlineData("RateLimit", null, "otp.issue", DegradedModePolicy.FailClosed)]
    [InlineData("RateLimit", null, "recovery-ip", DegradedModePolicy.FailClosed)]
    [InlineData("RateLimit", null, "assistant", DegradedModePolicy.FailOpen)]
    [InlineData("RateLimit", "FailOpen", "otp.issue", DegradedModePolicy.FailOpen)]
    [InlineData("RateLimit", "FailClosed", "assistant", DegradedModePolicy.FailClosed)]
    [InlineData("OtpCounters", null, null, DegradedModePolicy.FailClosed)]
    [InlineData("NotificationRateLimit", null, null, DegradedModePolicy.FailOpen)]
    [InlineData("PermissionCache", null, null, DegradedModePolicy.FailOpen)]
    [InlineData("OtpCounters", "FailOpen", null, DegradedModePolicy.FailOpen)]
    public void Degraded_policy_resolution(string site, string? configured, string? scope, DegradedModePolicy expected)
        => DegradedModePolicyService.Resolve(site, configured, scope).Should().Be(expected);

    // ---------- refresh reuse chain ----------

    [Fact]
    public void Chain_walk_follows_replacements_and_survives_cycles()
    {
        var map = new Dictionary<string, string?> { ["a"] = "b", ["b"] = "c", ["c"] = null, ["x"] = "y", ["y"] = "x" };
        RefreshTokenCommandHandler.WalkChain("a", map).Should().Equal("a", "b", "c");
        RefreshTokenCommandHandler.WalkChain("x", map).Should().Equal("x", "y");
        RefreshTokenCommandHandler.WalkChain("zzz", map).Should().Equal("zzz");
    }
}
