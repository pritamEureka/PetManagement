using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Tracking timeline (checkpoints + proof of delivery). Status side effects on the
/// order / delivery assignment are delegated to IDeliveryStatusApplier (DeliveryService).
/// </summary>
public class DeliveryTrackingService : IDeliveryTrackingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IDeliveryStatusApplier _applier;
    private readonly IAuditLogger _audit;

    public DeliveryTrackingService(ApplicationDbContext db, ICurrentUserService current,
        IDeliveryStatusApplier applier, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _applier = applier;
        _audit = audit;
    }

    // Delivery.Edit is granted to every DeliveryUser, so it must NOT count as admin here:
    // only Delivery.Assign (dispatcher/admin) and Orders.View grant cross-order access.
    private bool IsStaff =>
        _current.Permissions.Contains(Permissions.Orders.View)
        || _current.Permissions.Contains(Permissions.Delivery.Assign);

    private bool IsDeliveryAdmin => _current.Permissions.Contains(Permissions.Delivery.Assign);

    public async Task<OrderTrackingDto> GetTimelineAsync(Guid orderId, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);

        var allowed = IsStaff || order.UserId == uid
            || await _db.OrderItems.AsNoTracking().AnyAsync(i => i.OrderId == orderId && i.Store.OwnerUserId == uid, ct)
            || await _db.DeliveryAssignments.AsNoTracking().AnyAsync(a => a.OrderId == orderId && a.DeliveryUserId == uid, ct);
        if (!allowed) throw new ForbiddenException();

        var cps = await _db.Set<DeliveryCheckpoint>().AsNoTracking()
            .Where(c => c.OrderId == orderId)
            .OrderBy(c => c.OccurredAt).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var proof = await _db.Set<DeliveryProof>().AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        return new OrderTrackingDto(
            order.Id, order.OrderNumber, order.ShipmentStatus.ToString(), order.TrackingNumber,
            order.Courier?.ToString(),
            cps.Select(c => new DeliveryCheckpointDto(c.Id, c.Status, c.Description, c.LocationText,
                c.Latitude, c.Longitude, c.Source, c.Provider, c.OccurredAt)).ToList(),
            proof is null ? null : new DeliveryProofDto(proof.Id, proof.PhotoUrl, proof.SignatureName,
                !string.IsNullOrEmpty(proof.OtpCode), proof.RecipientName, proof.CapturedAt));
    }

    public async Task<Guid> AddCheckpointAsync(Guid orderId, AddCheckpointInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var assignment = await EnsureCanWriteAsync(orderId, uid, ct);
        ValidateCoordinates(input.Latitude, input.Longitude);

        var cp = new DeliveryCheckpoint
        {
            OrderId = orderId,
            DeliveryAssignmentId = assignment?.Id,
            Status = input.Status,
            Description = Trim(input.Description, 500),
            LocationText = Trim(input.LocationText, 300),
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Source = CheckpointSource.Manual,
            OccurredAt = input.OccurredAt.HasValue ? AsUtc(input.OccurredAt.Value) : DateTime.UtcNow,
            CreatedBy = uid
        };
        _db.Set<DeliveryCheckpoint>().Add(cp);
        await _db.SaveChangesAsync(ct);

        await _applier.ApplyTrackingStatusAsync(orderId, input.Status, cp.Description, ct);
        await _audit.LogAsync("delivery.checkpoint", "Order", orderId.ToString(), input.Status.ToString(), ct: ct);
        return cp.Id;
    }

    public async Task AddProofAsync(Guid orderId, AddDeliveryProofInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var assignment = await EnsureCanWriteAsync(orderId, uid, ct);

        if (string.IsNullOrWhiteSpace(input.PhotoUrl) && string.IsNullOrWhiteSpace(input.SignatureName)
            && string.IsNullOrWhiteSpace(input.OtpCode))
            throw new ValidationException(new Dictionary<string, string[]>
                { ["proof"] = new[] { "Provide a photo URL, signature name or OTP code." } });

        var photo = Trim(input.PhotoUrl, 1024);
        if (photo is not null && !(photo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                                   || photo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                                   || photo.StartsWith("/")))
            throw new ValidationException(new Dictionary<string, string[]>
                { ["photoUrl"] = new[] { "Photo URL must be an http(s) or site-relative URL." } });

        var proof = await _db.Set<DeliveryProof>().FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        if (proof is null)
        {
            proof = new DeliveryProof { OrderId = orderId };
            _db.Set<DeliveryProof>().Add(proof);
        }
        proof.DeliveryAssignmentId = assignment?.Id;
        proof.PhotoUrl = photo;
        proof.SignatureName = Trim(input.SignatureName, 200);
        proof.OtpCode = Trim(input.OtpCode, 16);
        proof.RecipientName = Trim(input.RecipientName, 200);
        proof.CapturedAt = DateTime.UtcNow;
        proof.CapturedByUserId = uid;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("delivery.proof", "Order", orderId.ToString(), null, ct: ct);
    }

    public async Task<bool> IngestExternalEventAsync(ExternalTrackingEvent evt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(evt.TrackingNumber)) return false;

        var tn = evt.TrackingNumber.Trim();
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.TrackingNumber == tn)
            .Select(o => new { o.Id, o.Courier })
            .FirstOrDefaultAsync(ct);
        if (order is null) return false;
        if (order.Courier.HasValue
            && !string.Equals(order.Courier.Value.ToString(), evt.Provider, StringComparison.OrdinalIgnoreCase))
            return false;

        var eventId = string.IsNullOrWhiteSpace(evt.ExternalEventId)
            ? SyntheticEventId(evt)
            : evt.ExternalEventId.Trim();
        if (eventId.Length > 128) eventId = eventId[..128];
        var provider = evt.Provider.Length > 64 ? evt.Provider[..64] : evt.Provider;

        var duplicate = await _db.Set<DeliveryCheckpoint>().AsNoTracking()
            .AnyAsync(c => c.Provider == provider && c.ExternalEventId == eventId, ct);
        if (duplicate) return false;

        var assignmentId = await _db.DeliveryAssignments.AsNoTracking()
            .Where(a => a.OrderId == order.Id).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

        var cp = new DeliveryCheckpoint
        {
            OrderId = order.Id,
            DeliveryAssignmentId = assignmentId,
            Status = evt.Status,
            Description = Trim(evt.Description, 500),
            LocationText = Trim(evt.LocationText, 300),
            Latitude = evt.Latitude,
            Longitude = evt.Longitude,
            Source = CheckpointSource.Webhook,
            Provider = provider,
            ExternalEventId = eventId,
            OccurredAt = AsUtc(evt.OccurredAt)
        };
        _db.Set<DeliveryCheckpoint>().Add(cp);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate delivery of the same event: the unique index won.
            _db.Entry(cp).State = EntityState.Detached;
            return false;
        }

        await _applier.ApplyTrackingStatusAsync(order.Id, evt.Status, cp.Description, ct);
        return true;
    }

    // ---------------------------------------------------------------------------

    private async Task<DeliveryAssignment?> EnsureCanWriteAsync(Guid orderId, Guid uid, CancellationToken ct)
    {
        if (!await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == orderId, ct))
            throw new NotFoundException("Order", orderId);
        var assignment = await _db.DeliveryAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.OrderId == orderId, ct);
        if (!IsDeliveryAdmin && (assignment is null || assignment.DeliveryUserId != uid))
            throw new ForbiddenException();
        return assignment;
    }

    private static void ValidateCoordinates(double? lat, double? lng)
    {
        if (lat.HasValue && (lat < -90 || lat > 90))
            throw new ValidationException(new Dictionary<string, string[]> { ["latitude"] = new[] { "Latitude must be between -90 and 90." } });
        if (lng.HasValue && (lng < -180 || lng > 180))
            throw new ValidationException(new Dictionary<string, string[]> { ["longitude"] = new[] { "Longitude must be between -180 and 180." } });
    }

    private static string? Trim(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        return s.Length > max ? s[..max] : s;
    }

    internal static DateTime AsUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
    };

    private static string SyntheticEventId(ExternalTrackingEvent e)
    {
        var raw = $"{e.Provider}|{e.TrackingNumber}|{e.Status}|{AsUtc(e.OccurredAt):O}|{e.LocationText}";
        return "syn_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..40];
    }
}
