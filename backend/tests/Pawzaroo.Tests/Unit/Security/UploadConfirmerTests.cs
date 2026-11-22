using FluentAssertions;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Infrastructure.Modules.Security;
using Pawzaroo.Infrastructure.Storage;
using Xunit;

namespace Pawzaroo.Tests.Unit.Security;

public class UploadConfirmerTests
{
    private sealed class FakeStorage : IObjectStorage
    {
        public readonly Dictionary<string, (byte[] Data, string Ct)> Objects = new();
        public readonly List<string> Deleted = new();
        public Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        { var ms = new MemoryStream(); content.CopyTo(ms); Objects[key] = (ms.ToArray(), contentType); return Task.FromResult("https://x/" + key); }
        public Task<PresignedUpload> CreatePresignedUploadAsync(string key, string contentType, TimeSpan? expiresIn = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<string> CreatePresignedDownloadAsync(string key, TimeSpan? expiresIn = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct = default) { Objects.Remove(key); Deleted.Add(key); return Task.CompletedTask; }
        public Task<ObjectHead?> HeadAsync(string key, CancellationToken ct = default)
            => Task.FromResult<ObjectHead?>(Objects.TryGetValue(key, out var o) ? new ObjectHead(o.Data.Length, o.Ct, null) : null);
        public Task<byte[]> ReadRangeAsync(string key, long from, int length, CancellationToken ct = default)
            => Task.FromResult(Objects[key].Data.Skip((int)from).Take(length).ToArray());
        public string GetUrl(string key) => "https://x/" + key;
    }

    private static readonly Guid Uid = Guid.NewGuid();
    private string Key(string name = "abc.jpg") => $"u/{Uid}/2025/01/{name}";

    [Fact]
    public async Task Foreign_key_is_forbidden_and_not_deleted()
    {
        var s = new FakeStorage();
        var other = $"u/{Guid.NewGuid()}/2025/01/abc.jpg";
        s.Objects[other] = (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg");
        var r = await UploadConfirmer.ConfirmAsync(s, new FileValidationService(), Uid, other, "image/jpeg", default);
        r.Status.Should().Be(403);
        s.Deleted.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_object_is_404()
    {
        var r = await UploadConfirmer.ConfirmAsync(new FakeStorage(), new FileValidationService(), Uid, Key(), "image/jpeg", default);
        r.Status.Should().Be(404);
    }

    [Fact]
    public async Task Executable_disguised_as_jpeg_is_rejected_and_deleted()
    {
        var s = new FakeStorage();
        var bytes = new byte[2048]; bytes[0] = 0x4D; bytes[1] = 0x5A;
        s.Objects[Key()] = (bytes, "image/jpeg");
        var r = await UploadConfirmer.ConfirmAsync(s, new FileValidationService(), Uid, Key(), "image/jpeg", default);
        r.Ok.Should().BeFalse();
        r.Status.Should().Be(422);
        s.Deleted.Should().Contain(Key());
        s.Objects.Should().NotContainKey(Key());
    }

    [Fact]
    public async Task Stored_content_type_mismatch_is_rejected_and_deleted()
    {
        var s = new FakeStorage();
        s.Objects[Key()] = (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0 }, "text/html");
        var r = await UploadConfirmer.ConfirmAsync(s, new FileValidationService(), Uid, Key(), "image/jpeg", default);
        r.Status.Should().Be(422);
        s.Deleted.Should().Contain(Key());
    }
}
