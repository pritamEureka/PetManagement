using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.LogisticsPlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Features;

/// <summary>
/// Reverse logistics for APPROVED returns: buyer schedules a pickup window, staff assign a courier (DeliveryUser),
/// the courier updates progress and the store confirms receipt. This tracks logistics only; refund semantics
/// stay in ReturnService (approval) and the ledger.
/// </summary>
public class ReturnPickupService : IReturnPickupService
{
    public const int MaxReschedules = 3;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;
    private readonly IAuditLogger _audit;

    public ReturnPickupService(ApplicationDbContext db, ICurrentUserService current, INotificationService notify, IAuditLogger audit)
    { _db = db; _current = current; _notify = notify; _audit = audit; }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private bool IsDispatcher => _current.Permissions.Contains(Permissions.Delivery.Assign)
                                 || _current.Permissions.Contains(Permissions.Delivery.Edit);

    private static ValidationException Invalid(string f, string m) => new(new Dictionary<string, string[]> { [f] = new[] { m } });

    /// <summary>Pure window check (also unit tested): future start, end after start, at most 12h wide, within 14 days.</summary>
    public static string? ValidateWindow(DateTime start, DateTime end, DateTime now)
    {
        if (start <= now) return "Pickup window must start in the future.";
        if (end <= start) return "Pickup window end must be after its start.";
        if (end - start > TimeSpan.FromHours(12)) return "Pickup window can be at most 12 hours wide.";
        if (start > now.AddDays(14)) return "Pickup must be within the next 14 days.";
        return null;
    }

    private async Task<ReturnRequest> LoadReturnAsync(Guid id, CancellationToken ct) =>
        await _db.ReturnRequests.Include(r => r.OrderItem).ThenInclude(i => i.Order)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("ReturnRequest", id);

    private async Task<(ReturnPickup Pickup, ReturnRequest Return)> LoadPickupAsync(Guid pickupId, CancellationToken ct)
    {
        var p = await _db.Set<ReturnPickup>().FirstOrDefaultAsync(x => x.Id == pickupId, ct) ?? throw new NotFoundException("ReturnPickup", pickupId);
        return (p, await LoadReturnAsync(p.ReturnRequestId, ct));
    }

    private void AddCheckpoint(ReturnPickup p, string? note) =>
        _db.Set<ReturnPickupCheckpoint>().Add(new ReturnPickupCheckpoint
        { ReturnPickupId = p.Id, Status = p.Status, Note = note, ActorUserId = _current.UserId });

    private async Task<Guid?> StoreOwnerAsync(Guid storeId, CancellationToken ct) =>
        await _db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => (Guid?)s.OwnerUserId).FirstOrDefaultAsync(ct);

    private async Task SafeNotifyAsync(Guid? userId, string title, string body, object? payload, CancellationToken ct)
    {
        if (userId is null) return;
        try { await _notify.NotifyUserAsync(userId.Value, title, body, payload, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* notifications are best effort */ }
    }

    // ------------------------------------------------------------------ buyer

    public async Task<ReturnPickupDto> RequestAsync(Guid returnRequestId, RequestReturnPickupInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var rr = await LoadReturnAsync(returnRequestId, ct);
        if (rr.UserId != uid) throw new ForbiddenException();
        if (rr.Status != ApprovalStatus.Approved) throw new ConflictException("A pickup can only be requested after the return is approved.");
        if (await _db.Set<ReturnPickup>().AnyAsync(p => p.ReturnRequestId == returnRequestId, ct))
            throw new ConflictException("A pickup already exists for this return; reschedule it instead.");
        var err = ValidateWindow(input.WindowStart, input.WindowEnd, DateTime.UtcNow);
        if (err is not null) throw Invalid("windowStart", err);

        var address = string.IsNullOrWhiteSpace(input.Address) ? rr.OrderItem.Order.ShippingAddress : input.Address.Trim();
        var p = new ReturnPickup
        {
            ReturnRequestId = returnRequestId, ScheduledWindowStart = input.WindowStart, ScheduledWindowEnd = input.WindowEnd,
            AddressSnapshot = address.Length > 1000 ? address[..1000] : address,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(), Status = ReturnPickupStatus.Requested
        };
        _db.Set<ReturnPickup>().Add(p);
        AddCheckpoint(p, "Pickup requested");
        await _db.SaveChangesAsync(ct);

        await SafeNotifyAsync(await StoreOwnerAsync(rr.OrderItem.StoreId, ct), "Return pickup requested",
            $"A buyer scheduled a return pickup for order {rr.OrderItem.Order.OrderNumber}.", new { returnRequestId, pickupId = p.Id }, ct);
        await _audit.LogAsync("return_pickup.request", "ReturnPickup", p.Id.ToString(), null, ct: ct);
        return (await ToDtosAsync(new[] { p }, ct))[0];
    }

    public async Task<ReturnPickupDto> RescheduleAsync(Guid pickupId, ReschedulePickupInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var (p, rr) = await LoadPickupAsync(pickupId, ct);
        if (rr.UserId != uid) throw new ForbiddenException();
        if (p.Status is not (ReturnPickupStatus.Requested or ReturnPickupStatus.Scheduled or ReturnPickupStatus.Failed))
            throw new ConflictException("This pickup can no longer be rescheduled.");
        if (p.RescheduleCount >= MaxReschedules) throw new ConflictException("Reschedule limit reached; contact support.");
        var err = ValidateWindow(input.WindowStart, input.WindowEnd, DateTime.UtcNow);
        if (err is not null) throw Invalid("windowStart", err);

        p.ScheduledWindowStart = input.WindowStart; p.ScheduledWindowEnd = input.WindowEnd;
        if (!string.IsNullOrWhiteSpace(input.Notes)) p.Notes = input.Notes.Trim();
        p.RescheduleCount++;
        if (p.Status == ReturnPickupStatus.Failed)   // buyer re-requested after a failed attempt: back to the dispatch queue
        { p.Status = ReturnPickupStatus.Requested; p.DeliveryUserId = null; p.AssignedAt = null; }
        p.UpdatedAt = DateTime.UtcNow;
        AddCheckpoint(p, $"Rescheduled to {input.WindowStart:u}");
        await _db.SaveChangesAsync(ct);

        await SafeNotifyAsync(p.DeliveryUserId, "Return pickup rescheduled", $"New window starts {input.WindowStart:u} UTC.", new { pickupId }, ct);
        await SafeNotifyAsync(await StoreOwnerAsync(rr.OrderItem.StoreId, ct), "Return pickup rescheduled",
            $"Pickup for order {rr.OrderItem.Order.OrderNumber} moved to {input.WindowStart:u} UTC.", new { pickupId }, ct);
        await _audit.LogAsync("return_pickup.reschedule", "ReturnPickup", p.Id.ToString(), null, ct: ct);
        return (await ToDtosAsync(new[] { p }, ct))[0];
    }

    public async Task<ReturnPickupDto?> GetByReturnAsync(Guid returnRequestId, CancellationToken ct = default)
    {
        var uid = Uid();
        var rr = await LoadReturnAsync(returnRequestId, ct);
        var owner = await StoreOwnerAsync(rr.OrderItem.StoreId, ct);
        if (rr.UserId != uid && owner != uid && !IsDispatcher && !_current.Permissions.Contains(Permissions.Orders.View))
            throw new ForbiddenException();
        var p = await _db.Set<ReturnPickup>().AsNoTracking().FirstOrDefaultAsync(x => x.ReturnRequestId == returnRequestId, ct);
        return p is null ? null : (await ToDtosAsync(new[] { p }, ct))[0];
    }

    // ------------------------------------------------------------------ dispatch

    public async Task<PageResult<ReturnPickupDto>> ListAdminAsync(ReturnPickupStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!IsDispatcher && !_current.Permissions.Contains(Permissions.Delivery.View)) throw new ForbiddenException();
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Set<ReturnPickup>().AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(p => p.Status == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(p => p.ScheduledWindowStart).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<ReturnPickupDto>(await ToDtosAsync(rows, ct), total, page, pageSize);
    }

    public async Task<ReturnPickupDto> AssignAsync(Guid pickupId, AssignPickupInput input, CancellationToken ct = default)
    {
        if (!IsDispatcher) throw new ForbiddenException();
        var (p, rr) = await LoadPickupAsync(pickupId, ct);
        if (p.Status is not (ReturnPickupStatus.Requested or ReturnPickupStatus.Scheduled or ReturnPickupStatus.Failed))
            throw new ConflictException("This pickup is already in progress or closed.");
        var isCourier = await _db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == input.DeliveryUserId && ur.Role.Name == SystemRoles.DeliveryUser, ct);
        if (!isCourier) throw Invalid("deliveryUserId", "User does not have the DeliveryUser role.");

        p.DeliveryUserId = input.DeliveryUserId; p.AssignedAt = DateTime.UtcNow; p.Status = ReturnPickupStatus.Scheduled;
        p.UpdatedAt = DateTime.UtcNow;
        AddCheckpoint(p, "Courier assigned");
        await _db.SaveChangesAsync(ct);

        await SafeNotifyAsync(input.DeliveryUserId, "Return pickup assigned",
            $"Pick up a return for order {rr.OrderItem.Order.OrderNumber} between {p.ScheduledWindowStart:u} and {p.ScheduledWindowEnd:u} UTC.", new { pickupId }, ct);
        await SafeNotifyAsync(rr.UserId, "Return pickup scheduled", "A courier has been assigned to collect your return.", new { pickupId }, ct);
        await _audit.LogAsync("return_pickup.assign", "ReturnPickup", p.Id.ToString(), input.DeliveryUserId.ToString(), ct: ct);
        return (await ToDtosAsync(new[] { p }, ct))[0];
    }

    // ------------------------------------------------------------------ courier

    public async Task<IReadOnlyList<ReturnPickupDto>> ListMineAsync(bool includeClosed, CancellationToken ct = default)
    {
        var uid = Uid();
        var q = _db.Set<ReturnPickup>().AsNoTracking().Where(p => p.DeliveryUserId == uid);
        if (!includeClosed) q = q.Where(p => p.Status == ReturnPickupStatus.Scheduled || p.Status == ReturnPickupStatus.PickedUp || p.Status == ReturnPickupStatus.InTransit);
        var rows = await q.OrderBy(p => p.ScheduledWindowStart).Take(200).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<ReturnPickupDto> CourierUpdateAsync(Guid pickupId, CourierPickupUpdateInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var (p, rr) = await LoadPickupAsync(pickupId, ct);
        if (p.DeliveryUserId != uid) throw new ForbiddenException();
        if (input.Status is not (ReturnPickupStatus.PickedUp or ReturnPickupStatus.InTransit or ReturnPickupStatus.Failed))
            throw Invalid("status", "Couriers can set PickedUp, InTransit or Failed; the store confirms receipt.");
        if (!ReturnPickupMath.CanTransition(p.Status, input.Status))
            throw new ConflictException($"Cannot move a pickup from {p.Status} to {input.Status}.");

        p.Status = input.Status; p.UpdatedAt = DateTime.UtcNow;
        if (input.Status == ReturnPickupStatus.PickedUp)
        {
            p.PickedUpAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(input.ProofPhotoUrl)) p.ProofPhotoUrl = input.ProofPhotoUrl.Trim();
            if (!string.IsNullOrWhiteSpace(input.ProofSignatureName)) p.ProofSignatureName = input.ProofSignatureName.Trim();
        }
        AddCheckpoint(p, input.Note);
        await _db.SaveChangesAsync(ct);

        var owner = await StoreOwnerAsync(rr.OrderItem.StoreId, ct);
        var msg = input.Status switch
        {
            ReturnPickupStatus.PickedUp => "Your return was picked up.",
            ReturnPickupStatus.InTransit => "Your return is on its way to the store.",
            _ => "The pickup attempt failed. You can reschedule it."
        };
        await SafeNotifyAsync(rr.UserId, "Return pickup update", msg, new { pickupId, status = input.Status.ToString() }, ct);
        if (input.Status != ReturnPickupStatus.Failed)
            await SafeNotifyAsync(owner, "Return in transit", $"Return for order {rr.OrderItem.Order.OrderNumber}: {input.Status}.", new { pickupId }, ct);
        await _audit.LogAsync("return_pickup." + input.Status.ToString().ToLowerInvariant(), "ReturnPickup", p.Id.ToString(), input.Note, ct: ct);
        return (await ToDtosAsync(new[] { p }, ct))[0];
    }

    // ------------------------------------------------------------------ store

    public async Task<ReturnPickupDto> ConfirmReceiptAsync(Guid pickupId, string? note, CancellationToken ct = default)
    {
        var uid = Uid();
        var (p, rr) = await LoadPickupAsync(pickupId, ct);
        var owner = await StoreOwnerAsync(rr.OrderItem.StoreId, ct);
        if (owner != uid && !_current.Permissions.Contains(Permissions.Orders.Refund)) throw new ForbiddenException();
        if (!ReturnPickupMath.CanTransition(p.Status, ReturnPickupStatus.ReceivedByStore))
            throw new ConflictException("Only a picked-up return can be confirmed as received.");

        p.Status = ReturnPickupStatus.ReceivedByStore; p.ReceivedAt = DateTime.UtcNow; p.UpdatedAt = DateTime.UtcNow;
        AddCheckpoint(p, note);
        await _db.SaveChangesAsync(ct);

        await SafeNotifyAsync(rr.UserId, "Return received", $"The store received your return for order {rr.OrderItem.Order.OrderNumber}.", new { pickupId }, ct);
        await _audit.LogAsync("return_pickup.received", "ReturnPickup", p.Id.ToString(), note, ct: ct);
        return (await ToDtosAsync(new[] { p }, ct))[0];
    }

    // ------------------------------------------------------------------ mapping

    private async Task<List<ReturnPickupDto>> ToDtosAsync(IReadOnlyCollection<ReturnPickup> pickups, CancellationToken ct)
    {
        var pickupIds = pickups.Select(p => p.Id).ToList();
        var rrIds = pickups.Select(p => p.ReturnRequestId).ToList();
        var rrs = await _db.ReturnRequests.AsNoTracking().Include(r => r.OrderItem).ThenInclude(i => i.Order)
            .Include(r => r.OrderItem).ThenInclude(i => i.Product)
            .Where(r => rrIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var courierIds = pickups.Where(p => p.DeliveryUserId != null).Select(p => p.DeliveryUserId!.Value).Distinct().ToList();
        var couriers = await _db.Users.AsNoTracking().Where(u => courierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.DisplayName }).ToDictionaryAsync(u => u.Id, ct);
        var cps = (await _db.Set<ReturnPickupCheckpoint>().AsNoTracking().Where(c => pickupIds.Contains(c.ReturnPickupId))
            .OrderBy(c => c.OccurredAt).ToListAsync(ct)).ToLookup(c => c.ReturnPickupId);

        return pickups.Select(p =>
        {
            rrs.TryGetValue(p.ReturnRequestId, out var rr);
            string? courier = null;
            if (p.DeliveryUserId is { } cid && couriers.TryGetValue(cid, out var c)) courier = c.DisplayName ?? c.Email;
            return new ReturnPickupDto(p.Id, p.ReturnRequestId, rr?.OrderItem.OrderId ?? Guid.Empty, rr?.OrderItem.Order.OrderNumber,
                rr?.OrderItem.Product?.Name, p.Status, p.ScheduledWindowStart, p.ScheduledWindowEnd, p.AddressSnapshot, p.Notes,
                p.DeliveryUserId, courier, p.PickedUpAt, p.ReceivedAt, p.ProofPhotoUrl, p.ProofSignatureName, p.RescheduleCount, p.CreatedAt,
                cps[p.Id].Select(c => new ReturnPickupCheckpointDto(c.Id, c.Status, c.Note, c.OccurredAt)).ToList());
        }).ToList();
    }
}
