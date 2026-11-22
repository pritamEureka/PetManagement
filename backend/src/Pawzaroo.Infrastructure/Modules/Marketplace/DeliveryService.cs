using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.CommercePlus;
using Pawzaroo.Application.Modules.Marketplace.Dtos;
using Pawzaroo.Application.Modules.Marketplace.Events;
using Pawzaroo.Application.Modules.Marketplace.Services;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Store;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Marketplace;

public class DeliveryService : IDeliveryService, IDeliveryStatusApplier
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly INotificationService _notify;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;
    private readonly ILedgerRecorder _ledger;

    public DeliveryService(ApplicationDbContext db, ICurrentUserService current,
        INotificationService notify, IKafkaProducer kafka, IAuditLogger audit, ILedgerRecorder ledger)
    {
        _db = db;
        _current = current;
        _notify = notify;
        _kafka = kafka;
        _audit = audit;
        _ledger = ledger;
    }

    // -------- Admin --------------------------------------------------------------

    public async Task<Guid> AssignAsync(Guid orderId, AssignDeliveryInput input, CancellationToken ct = default)
    {
        EnsureAdmin();

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order", orderId);
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Denied)
            throw new ConflictException("Cannot assign a delivery to a closed order.");

        var isDelivery = await _db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == input.DeliveryUserId && ur.Role.Name == SystemRoles.DeliveryUser, ct);
        if (!isDelivery)
            throw new ValidationException(new Dictionary<string, string[]> { ["deliveryUserId"] = new[] { "User does not have the DeliveryUser role." } });

        var existing = await _db.DeliveryAssignments.FirstOrDefaultAsync(a => a.OrderId == orderId, ct);
        if (existing is not null)
        {
            // Reassign: route to a different delivery user. Keep history of the
            // status the previous person reached so this isn't a silent overwrite.
            existing.DeliveryUserId = input.DeliveryUserId;
            existing.Notes = input.Notes;
            existing.Status = DeliveryAssignmentStatus.Assigned;
            existing.AssignedAt = DateTime.UtcNow;
            existing.PickedUpAt = null;
            existing.DeliveredAt = null;
            existing.FailedAt = null;
            existing.AssignedByUserId = _current.UserId ?? Guid.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _current.UserId;
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            existing = new DeliveryAssignment
            {
                OrderId = orderId,
                DeliveryUserId = input.DeliveryUserId,
                Notes = input.Notes,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = _current.UserId ?? Guid.Empty,
                Status = DeliveryAssignmentStatus.Assigned
            };
            _db.DeliveryAssignments.Add(existing);
            await _db.SaveChangesAsync(ct);
        }

        // Bumping order shipment from NotShipped → Processing reflects "courier accepted".
        if (order.ShipmentStatus == ShipmentStatus.NotShipped)
        {
            order.ShipmentStatus = ShipmentStatus.Processing;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = _current.UserId;
            await _db.SaveChangesAsync(ct);
        }

        await _notify.NotifyUserAsync(input.DeliveryUserId, "New delivery assigned",
            $"Order {order.OrderNumber} is yours to deliver.",
            new { orderId = order.Id, assignmentId = existing.Id }, ct);
        await _audit.LogAsync("delivery.assign", "Order", order.Id.ToString(), input.DeliveryUserId.ToString(), ct: ct);

        return existing.Id;
    }

    public async Task<IReadOnlyList<DeliveryUserSummaryDto>> ListDeliveryUsersAsync(CancellationToken ct = default)
    {
        EnsureAdmin();

        var deliveryUserIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Name == SystemRoles.DeliveryUser)
            .Select(ur => ur.UserId).Distinct().ToListAsync(ct);

        return await _db.Users.AsNoTracking()
            .Where(u => deliveryUserIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new DeliveryUserSummaryDto(
                u.Id, u.DisplayName, u.Email, u.PhoneNumber,
                _db.DeliveryAssignments.Count(a => a.DeliveryUserId == u.Id
                    && a.Status != DeliveryAssignmentStatus.Delivered
                    && a.Status != DeliveryAssignmentStatus.Failed),
                _db.DeliveryAssignments.Count(a => a.DeliveryUserId == u.Id
                    && a.Status == DeliveryAssignmentStatus.Delivered)))
            .ToListAsync(ct);
    }

    public async Task<PageResult<DeliveryAssignmentDto>> ListAdminAsync(
        DeliveryAssignmentStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        EnsureAdmin();
        var q = _db.DeliveryAssignments.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(a => a.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.AssignedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => ToDto(a)).ToListAsync(ct);
        return new PageResult<DeliveryAssignmentDto>(items, total, page, pageSize);
    }

    // -------- Delivery user ------------------------------------------------------

    public async Task<IReadOnlyList<DeliveryAssignmentDto>> ListMineActiveAsync(CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        return await _db.DeliveryAssignments.AsNoTracking()
            .Where(a => a.DeliveryUserId == uid
                     && a.Status != DeliveryAssignmentStatus.Delivered
                     && a.Status != DeliveryAssignmentStatus.Failed)
            .OrderBy(a => a.AssignedAt)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    public async Task<PageResult<DeliveryAssignmentDto>> ListMineHistoryAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var q = _db.DeliveryAssignments.AsNoTracking()
            .Where(a => a.DeliveryUserId == uid
                    && (a.Status == DeliveryAssignmentStatus.Delivered
                     || a.Status == DeliveryAssignmentStatus.Failed));

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.UpdatedAt ?? a.AssignedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
        return new PageResult<DeliveryAssignmentDto>(items, total, page, pageSize);
    }

    public async Task UpdateStatusAsync(Guid assignmentId, UpdateDeliveryStatusInput input, CancellationToken ct = default)
    {
        var uid = _current.UserId ?? throw new ForbiddenException();
        var a = await _db.DeliveryAssignments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.Id == assignmentId, ct)
            ?? throw new NotFoundException("DeliveryAssignment", assignmentId);

        var isAdmin = _current.Permissions.Contains(Permissions.Delivery.Edit)
                   || _current.Permissions.Contains(Permissions.Delivery.Assign);
        if (a.DeliveryUserId != uid && !isAdmin) throw new ForbiddenException();

        if (a.Status is DeliveryAssignmentStatus.Delivered or DeliveryAssignmentStatus.Failed)
            throw new ConflictException("Assignment is already closed.");

        a.Status = input.Status;
        if (!string.IsNullOrWhiteSpace(input.Notes)) a.Notes = input.Notes;
        a.UpdatedAt = DateTime.UtcNow;
        a.UpdatedBy = uid;

        switch (input.Status)
        {
            case DeliveryAssignmentStatus.PickedUp:
                a.PickedUpAt ??= DateTime.UtcNow;
                a.Order.ShipmentStatus = ShipmentStatus.InTransit;
                break;
            case DeliveryAssignmentStatus.InTransit:
                a.Order.ShipmentStatus = ShipmentStatus.InTransit;
                break;
            case DeliveryAssignmentStatus.OutForDelivery:
                a.Order.ShipmentStatus = ShipmentStatus.OutForDelivery;
                break;
            case DeliveryAssignmentStatus.Delivered:
                a.DeliveredAt = DateTime.UtcNow;
                a.Order.ShipmentStatus = ShipmentStatus.Delivered;
                a.Order.Status = OrderStatus.Delivered;
                break;
            case DeliveryAssignmentStatus.Failed:
                a.FailedAt = DateTime.UtcNow;
                a.Order.ShipmentStatus = ShipmentStatus.Failed;
                break;
        }

        a.Order.UpdatedAt = DateTime.UtcNow;
        a.Order.UpdatedBy = uid;

        // Tracking timeline: every manual status change is also a checkpoint.
        _db.Set<DeliveryCheckpoint>().Add(new DeliveryCheckpoint
        {
            OrderId = a.OrderId, DeliveryAssignmentId = a.Id, Source = CheckpointSource.Manual,
            Status = ToTrackingStatus(input.Status), Description = input.Notes, CreatedBy = uid
        });
        await _db.SaveChangesAsync(ct);

        if (input.Status == DeliveryAssignmentStatus.Delivered)
            await _ledger.RecordOrderEarningsAsync(a.OrderId, ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderShipmentStatusChanged(a.OrderId, a.Order.OrderNumber, a.Order.ShipmentStatus.ToString(), a.Order.TrackingNumber, DateTime.UtcNow),
            a.OrderId.ToString(), ct);

        await _notify.NotifyUserAsync(a.Order.UserId, "Delivery update",
            $"{a.Order.OrderNumber}: {input.Status}",
            new { orderId = a.OrderId, deliveryStatus = input.Status.ToString() }, ct);

        await _audit.LogAsync("delivery.status", "DeliveryAssignment", a.Id.ToString(),
            input.Status.ToString(), ct: ct);
    }

    // -------- System / courier-webhook driven updates -----------------------------

    private static TrackingStatus ToTrackingStatus(DeliveryAssignmentStatus s) => s switch
    {
        DeliveryAssignmentStatus.PickedUp => TrackingStatus.PickedUp,
        DeliveryAssignmentStatus.InTransit => TrackingStatus.InTransit,
        DeliveryAssignmentStatus.OutForDelivery => TrackingStatus.OutForDelivery,
        DeliveryAssignmentStatus.Delivered => TrackingStatus.Delivered,
        DeliveryAssignmentStatus.Failed => TrackingStatus.DeliveryFailed,
        _ => TrackingStatus.LabelCreated
    };

    private static int ShipmentRank(ShipmentStatus s) => s switch
    {
        ShipmentStatus.NotShipped => 0,
        ShipmentStatus.Processing => 1,
        ShipmentStatus.InTransit => 2,
        ShipmentStatus.OutForDelivery => 3,
        ShipmentStatus.Delivered => 4,
        _ => 5 // Failed
    };

    /// <summary>
    /// Applies a normalized tracking status to the order (and its assignment if any) without a
    /// user context. Forward-only: stale or out-of-order events never regress an order, and
    /// delivered orders are never touched. Notifies the buyer on key transitions.
    /// </summary>
    public async Task ApplyTrackingStatusAsync(Guid orderId, TrackingStatus status, string? note, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.DeliveryAssignment)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return;
        if (order.ShipmentStatus == ShipmentStatus.Delivered || order.Status is OrderStatus.Cancelled or OrderStatus.Denied)
            return;

        ShipmentStatus? target = status switch
        {
            TrackingStatus.PickedUp or TrackingStatus.InTransit => ShipmentStatus.InTransit,
            TrackingStatus.OutForDelivery => ShipmentStatus.OutForDelivery,
            TrackingStatus.Delivered => ShipmentStatus.Delivered,
            TrackingStatus.DeliveryFailed or TrackingStatus.Returned => ShipmentStatus.Failed,
            _ => null // LabelCreated / Exception: timeline only
        };
        if (target is null) return;
        if (target != ShipmentStatus.Failed && ShipmentRank(target.Value) <= ShipmentRank(order.ShipmentStatus)) return;
        if (target == ShipmentStatus.Failed && order.ShipmentStatus == ShipmentStatus.Failed) return;

        var now = DateTime.UtcNow;
        order.ShipmentStatus = target.Value;
        if (target == ShipmentStatus.Delivered) order.Status = OrderStatus.Delivered;
        else if (target is ShipmentStatus.InTransit or ShipmentStatus.OutForDelivery
                 && order.Status is OrderStatus.Created or OrderStatus.Confirmed or OrderStatus.Packed)
            order.Status = OrderStatus.Shipped;
        order.UpdatedAt = now;

        var a = order.DeliveryAssignment;
        if (a is not null && a.Status is not (DeliveryAssignmentStatus.Delivered or DeliveryAssignmentStatus.Failed))
        {
            switch (status)
            {
                case TrackingStatus.PickedUp:
                    a.Status = DeliveryAssignmentStatus.PickedUp; a.PickedUpAt ??= now; break;
                case TrackingStatus.InTransit:
                    a.Status = DeliveryAssignmentStatus.InTransit; break;
                case TrackingStatus.OutForDelivery:
                    a.Status = DeliveryAssignmentStatus.OutForDelivery; break;
                case TrackingStatus.Delivered:
                    a.Status = DeliveryAssignmentStatus.Delivered; a.DeliveredAt = now; break;
                case TrackingStatus.DeliveryFailed:
                case TrackingStatus.Returned:
                    a.Status = DeliveryAssignmentStatus.Failed; a.FailedAt = now; break;
            }
            if (!string.IsNullOrWhiteSpace(note)) a.Notes = note;
            a.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);

        if (target == ShipmentStatus.Delivered)
            await _ledger.RecordOrderEarningsAsync(orderId, ct);

        await _kafka.PublishAsync(MarketplaceTopics.OrderEvents,
            new OrderShipmentStatusChanged(order.Id, order.OrderNumber, order.ShipmentStatus.ToString(), order.TrackingNumber, now),
            order.Id.ToString(), ct);

        var (title, body) = status switch
        {
            TrackingStatus.PickedUp or TrackingStatus.InTransit => ("Order on the way", $"{order.OrderNumber} has been picked up and is in transit."),
            TrackingStatus.OutForDelivery => ("Out for delivery", $"{order.OrderNumber} is out for delivery today."),
            TrackingStatus.Delivered => ("Order delivered", $"{order.OrderNumber} was delivered."),
            TrackingStatus.Returned => ("Delivery returned", $"{order.OrderNumber} is being returned to the seller."),
            _ => ("Delivery attempt failed", $"A delivery attempt for {order.OrderNumber} failed.")
        };
        await _notify.NotifyUserAsync(order.UserId, title, body,
            new { orderId = order.Id, tracking = status.ToString() }, ct);
        await _audit.LogAsync("delivery.tracking", "Order", order.Id.ToString(), status.ToString(), ct: ct);
    }

    private void EnsureAdmin()
    {
        if (!_current.Permissions.Contains(Permissions.Delivery.Assign)
            && !_current.Permissions.Contains(Permissions.Delivery.Edit)
            && !_current.Permissions.Contains(Permissions.Orders.View))
            throw new ForbiddenException();
    }

    // EF-translatable projection — see note on OrderService.ToDto for the same constraint.
    private static DeliveryAssignmentDto ToDto(DeliveryAssignment a) => new(
        a.Id, a.OrderId, a.Order.OrderNumber, a.Order.Total,
        a.DeliveryUserId, a.DeliveryUser.DisplayName, a.DeliveryUser.PhoneNumber,
        a.Status, a.Notes,
        a.Order.ShippingAddress, a.Order.ShippingCity, a.Order.ShippingCountry,
        a.Order.User.DisplayName, a.Order.User.PhoneNumber,
        a.AssignedAt, a.PickedUpAt, a.DeliveredAt, a.FailedAt);
}
