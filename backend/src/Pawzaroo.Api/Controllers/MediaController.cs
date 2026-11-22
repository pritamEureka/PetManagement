using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Api.Filters;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;

namespace Pawzaroo.Api.Controllers;

public record PresignRequest(string FileName, string ContentType);
public record ConfirmUploadRequest(string Key, string DeclaredContentType);

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IObjectStorage _storage;
    private readonly ICurrentUserService _current;
    private readonly IFileValidationService _fileValidation;
    private readonly Pawzaroo.Infrastructure.Storage.IMediaUrlSigner _signer;

    public MediaController(IObjectStorage storage, ICurrentUserService current, IFileValidationService fileValidation,
        Pawzaroo.Infrastructure.Storage.IMediaUrlSigner signer)
    {
        _storage = storage;
        _current = current;
        _fileValidation = fileValidation;
        _signer = signer;
    }

    [HttpPost("presign")]
    public async Task<IActionResult> Presign([FromBody] PresignRequest req, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();

        if (string.IsNullOrWhiteSpace(req.FileName) || string.IsNullOrWhiteSpace(req.ContentType))
            return BadRequest(new { error = new { code = "invalid_request", message = "FileName and ContentType are required." } });

        var pre = _fileValidation.ValidatePreflight(req.FileName, req.ContentType);
        if (!pre.Allowed)
            return BadRequest(new { error = new { code = "invalid_file", message = pre.Reason ?? "File rejected." } });

        // Random key; extension is derived from the sanitised name and already passed the allow-list preflight.
        var ext = Path.GetExtension(Pawzaroo.Infrastructure.Modules.Security.UploadInspector.SanitizeFileName(req.FileName)).ToLowerInvariant();
        var key = Pawzaroo.Infrastructure.Modules.Security.UploadInspector.RandomKey($"u/{uid}", ext);
        var upload = await _storage.CreatePresignedUploadAsync(key, req.ContentType, TimeSpan.FromMinutes(15), ct);
        return Ok(upload);
    }

    /// <summary>
    /// Call after the browser's presigned PUT completes. Validates the stored bytes (magic bytes, polyglot, size, pixels,
    /// scanner), strips JPEG/PNG metadata when small, and DELETES the object + returns 422 when it is not acceptable.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmUploadRequest req, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var r = await Pawzaroo.Infrastructure.Storage.UploadConfirmer.ConfirmAsync(
            _storage, _fileValidation, uid, req.Key, req.DeclaredContentType, ct);
        if (r.Ok) return Ok(new { key = req.Key, url = r.Url });
        if (r.Status == 403) return Forbid();
        return StatusCode(r.Status, new { error = new { code = r.Code, message = r.Reason } });
    }

    /// <summary>Returns an expiring HMAC-signed proxy link for one of the caller's own objects.</summary>
    [HttpPost("sign")]
    public IActionResult Sign([FromBody] SignRequest req)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(req.Key) || req.Key.Contains("..") || !req.Key.StartsWith($"u/{uid}/", StringComparison.Ordinal))
            return Forbid();
        var minutes = Math.Clamp(req.Minutes ?? 15, 1, 24 * 60);
        return Ok(new { url = _signer.Sign(req.Key, TimeSpan.FromMinutes(minutes)) });
    }

    /// <summary>Verifies the HMAC + expiry and redirects to a short-lived presigned object URL.</summary>
    [HttpGet("signed")]
    [AllowAnonymous]
    [SkipApiResponseWrapping]
    public async Task<IActionResult> Signed([FromQuery] string key, [FromQuery] long exp, [FromQuery] string sig, CancellationToken ct)
    {
        if (!_signer.Verify(key, exp, sig)) return NotFound();
        var url = await _storage.CreatePresignedDownloadAsync(key, TimeSpan.FromMinutes(2), ct);
        return Redirect(url);
    }
}

public record SignRequest(string Key, int? Minutes);
