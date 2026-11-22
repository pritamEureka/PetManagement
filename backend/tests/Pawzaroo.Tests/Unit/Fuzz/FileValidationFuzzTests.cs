using FluentAssertions;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Infrastructure.Modules.Security;
using Xunit;

namespace Pawzaroo.Tests.Unit.Fuzz;

/// <summary>
/// Seeded randomized tests against the public IFileValidationService surface. Deliberately tolerant:
/// only invariants that must hold regardless of how the allowlist evolves.
/// </summary>
public class FileValidationFuzzTests
{
    private const int Iterations = 1000;
    private static IFileValidationService Svc() => new FileValidationService();

    private static readonly (string Ext, string Mime, byte[] Magic)[] Known =
    {
        // Realistic minimal headers (PNG signature + IHDR 16x16, GIF89a 16x16) so the pixel-limit check sees sane dimensions.
        (".png", "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 16, 0, 0, 0, 16 }),
        // SOI + APP0/JFIF + SOF0 (16x16): a minimal header with sane dimensions.
        (".jpg", "image/jpeg", new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x10, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        }),
        (".gif", "image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 16, 0, 16, 0 }),
        (".pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }),   // "%PDF-1.4"
    };

    private static byte[] Random(Random r, int len)
    {
        var b = new byte[len];
        r.NextBytes(b);
        return b;
    }

    [Fact]
    public void random_inputs_never_throw()
    {
        var r = new Random(1234);
        var svc = Svc();
        for (var i = 0; i < Iterations; i++)
        {
            var name = r.Next(4) == 0 ? "" : new string(Enumerable.Range(0, r.Next(1, 40)).Select(_ => (char)r.Next(0x20, 0x7F)).ToArray());
            var mime = r.Next(5) == 0 ? null : new string(Enumerable.Range(0, r.Next(0, 30)).Select(_ => (char)r.Next(0x20, 0x7F)).ToArray());
            var size = r.Next(6) switch { 0 => 0L, 1 => -1L, 2 => long.MaxValue, 3 => long.MinValue, _ => r.NextInt64(0, 40_000_000) };
            var head = Random(r, r.Next(0, 64));

            var act = () =>
            {
                svc.Validate(name, mime, size, head);
                svc.ValidatePreflight(name, mime);
            };
            act.Should().NotThrow($"iteration {i}: name='{name}' mime='{mime}' size={size}");
        }
    }

    [Fact]
    public void empty_or_negative_sizes_are_never_allowed()
    {
        var r = new Random(7);
        var svc = Svc();
        for (var i = 0; i < Iterations; i++)
        {
            var k = Known[r.Next(Known.Length)];
            var size = r.Next(2) == 0 ? 0 : -r.Next(1, 1_000_000);
            svc.Validate("file" + k.Ext, k.Mime, size, k.Magic).Allowed.Should().BeFalse();
        }
    }

    [Fact]
    public void valid_magic_header_with_matching_extension_and_mime_is_accepted()
    {
        var r = new Random(99);
        var svc = Svc();
        for (var i = 0; i < Iterations; i++)
        {
            var k = Known[r.Next(Known.Length)];
            var head = k.Magic.Concat(Random(r, r.Next(0, 32))).ToArray();
            var res = svc.Validate($"photo{r.Next()}{k.Ext}", k.Mime, r.Next(1, 5_000_000), head);
            res.Allowed.Should().BeTrue($"{k.Ext} with a genuine header should pass (reason: {res.Reason})");
        }
    }

    [Fact]
    public void random_bytes_without_the_magic_header_are_rejected_for_signature_checked_types()
    {
        var r = new Random(555);
        var svc = Svc();
        for (var i = 0; i < Iterations; i++)
        {
            var k = Known[r.Next(Known.Length)];
            var head = Random(r, 32);
            head[0] = (byte)(k.Magic[0] ^ 0xFF);   // guarantee the first byte differs from the signature
            svc.Validate("x" + k.Ext, k.Mime, 1024, head).Allowed.Should().BeFalse();
        }
    }

    [Fact]
    public void executable_extensions_are_rejected_regardless_of_claimed_mime_or_header()
    {
        var r = new Random(31337);
        var svc = Svc();
        var exts = new[] { ".exe", ".dll", ".sh", ".bat", ".js", ".php", ".html" };
        for (var i = 0; i < Iterations; i++)
        {
            var k = Known[r.Next(Known.Length)];
            var ext = exts[r.Next(exts.Length)];
            svc.Validate("payload" + ext, k.Mime, 1024, k.Magic).Allowed.Should().BeFalse($"{ext} must never pass");
            svc.ValidatePreflight("payload" + ext, k.Mime).Allowed.Should().BeFalse();
        }
    }

    [Fact]
    public void mime_extension_mismatch_is_rejected_in_preflight()
    {
        var svc = Svc();
        svc.ValidatePreflight("a.png", "application/pdf").Allowed.Should().BeFalse();
        svc.ValidatePreflight("a.pdf", "image/png").Allowed.Should().BeFalse();
        svc.ValidatePreflight("a.png", null).Allowed.Should().BeFalse();
    }
}
