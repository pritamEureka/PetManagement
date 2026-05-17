using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Api.Authorization;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Infrastructure.Persistence;

namespace Pawzaroo.Api.Controllers.V1;

/// <summary>
/// Key/value system settings. Anything in here is a *soft* config — runtime
/// switches, feature flags, branding strings. Hard config (connection strings,
/// JWT key) stays in appsettings / secrets.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/settings")]
[Authorize]
public class AdminSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public AdminSettingsController(ApplicationDbContext db) => _db = db;

    public record SettingRow(string Key, string Category, string ValueJson,
                             string? Description, bool IsSecret, DateTime? UpdatedAt);
    public record UpsertSettingBody(string ValueJson, string? Category, string? Description, bool? IsSecret);

    [HttpGet]
    [Permission(Permissions.Settings.View)]
    public async Task<IReadOnlyList<SettingRow>> List(CancellationToken ct)
        => await _db.SystemSettings.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            // Mask secret values from the list; secret reveals are a separate endpoint.
            .Select(s => new SettingRow(s.Key, s.Category,
                s.IsSecret ? "***" : s.ValueJson,
                s.Description, s.IsSecret, s.UpdatedAt))
            .ToListAsync(ct);

    [HttpPut("{key}")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Upsert(string key, [FromBody] UpsertSettingBody body, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            row = new SystemSetting { Key = key };
            _db.SystemSettings.Add(row);
        }
        row.ValueJson = body.ValueJson;
        if (!string.IsNullOrWhiteSpace(body.Category))    row.Category = body.Category!;
        if (!string.IsNullOrWhiteSpace(body.Description)) row.Description = body.Description;
        if (body.IsSecret.HasValue)                       row.IsSecret = body.IsSecret.Value;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{key}")]
    [Permission(Permissions.Settings.Edit)]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        var row = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is not null) { _db.SystemSettings.Remove(row); await _db.SaveChangesAsync(ct); }
        return NoContent();
    }
}
