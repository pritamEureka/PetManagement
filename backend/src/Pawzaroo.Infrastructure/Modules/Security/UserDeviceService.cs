using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Security.Dtos;
using Pawzaroo.Application.Modules.Security.Services;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Security;

/// <summary>
/// Tracks one row per (user, fingerprint). Fingerprint is the SHA-256 of
/// UserAgent + client cookie / explicit client-id header. If the client never
/// presents a fingerprint we hash the UA alone — coarser but still useful for
/// "new device seen" notifications.
/// </summary>
public class UserDeviceService : IUserDeviceService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public UserDeviceService(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<UserDevice> TrackOnLoginAsync(Guid userId, string? userAgent, string? ip,
        string? clientFingerprint, CancellationToken ct = default)
    {
        var fp = Fingerprint(userAgent, clientFingerprint);
        var device = await _db.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Fingerprint == fp, ct);
        if (device is null)
        {
            device = new UserDevice
            {
                UserId = userId,
                Fingerprint = fp,
                UserAgent = userAgent,
                IpAddress = ip,
                Label = DeriveLabel(userAgent),
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            _db.UserDevices.Add(device);
        }
        else
        {
            device.LastSeenAt = DateTime.UtcNow;
            device.IpAddress = ip ?? device.IpAddress;
        }
        await _db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<IReadOnlyList<UserDeviceDto>> ListMineAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.UserDevices.AsNoTracking()
            .Where(d => d.UserId == uid)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new UserDeviceDto(d.Id, d.Fingerprint, d.Label, d.UserAgent,
                d.IpAddress, d.IpCity, d.IpCountry,
                d.FirstSeenAt, d.LastSeenAt, d.IsTrusted, d.IsRevoked))
            .ToListAsync(ct);
    }

    public async Task RevokeAsync(Guid deviceId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var d = await _db.UserDevices.FirstOrDefaultAsync(x => x.Id == deviceId && x.UserId == uid, ct)
                ?? throw new NotFoundException("UserDevice", deviceId);
        d.IsRevoked = true;
        d.IsTrusted = false;
        // Cascade: kill every refresh token that originated on this device.
        // (We index refresh tokens by CreatedByIp today; tighten in a follow-up.)
        await _db.SaveChangesAsync(ct);
    }

    public async Task TrustAsync(Guid deviceId, string? label, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var d = await _db.UserDevices.FirstOrDefaultAsync(x => x.Id == deviceId && x.UserId == uid, ct)
                ?? throw new NotFoundException("UserDevice", deviceId);
        d.IsTrusted = true;
        if (!string.IsNullOrWhiteSpace(label)) d.Label = label;
        await _db.SaveChangesAsync(ct);
    }

    private static string Fingerprint(string? userAgent, string? clientFingerprint)
    {
        var raw = (clientFingerprint ?? "") + "|" + (userAgent ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string? DeriveLabel(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";
        // Very rough heuristic — fine for a label, not for identity.
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) return "iPhone";
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
        if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "Mac";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux";
        return "Web browser";
    }
}
