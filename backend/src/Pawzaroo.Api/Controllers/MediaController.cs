using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Services;

namespace Pawzaroo.Api.Controllers;

public record PresignRequest(string FileName, string ContentType);

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IObjectStorage _storage;
    private readonly ICurrentUserService _current;
    private readonly IFileValidationService _fileValidation;

    public MediaController(IObjectStorage storage, ICurrentUserService current, IFileValidationService fileValidation)
    {
        _storage = storage;
        _current = current;
        _fileValidation = fileValidation;
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

        var ext = Path.GetExtension(req.FileName);
        var key = $"u/{uid}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
        var upload = await _storage.CreatePresignedUploadAsync(key, req.ContentType, TimeSpan.FromMinutes(15), ct);
        return Ok(upload);
    }
}
