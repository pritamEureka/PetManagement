using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pawzaroo.Application.Common.Interfaces;

namespace Pawzaroo.Api.Controllers;

public record PresignRequest(string FileName, string ContentType);

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IObjectStorage _storage;
    private readonly ICurrentUserService _current;

    public MediaController(IObjectStorage storage, ICurrentUserService current)
    {
        _storage = storage;
        _current = current;
    }

    [HttpPost("presign")]
    public async Task<IActionResult> Presign([FromBody] PresignRequest req, CancellationToken ct)
    {
        var uid = _current.UserId ?? throw new UnauthorizedAccessException();
        var ext = Path.GetExtension(req.FileName);
        var key = $"u/{uid}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
        var upload = await _storage.CreatePresignedUploadAsync(key, req.ContentType, TimeSpan.FromMinutes(15), ct);
        return Ok(upload);
    }
}
