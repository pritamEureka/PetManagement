using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Common.Permissions;
using Pawzaroo.Application.Modules.PetServices;
using Pawzaroo.Application.Modules.Pets.Events;
using Pawzaroo.Domain.Admin;
using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Notifications;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Services;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;
using static Pawzaroo.Infrastructure.Modules.Pets.PetService;

namespace Pawzaroo.Infrastructure.Modules.PetServices;

/// <summary>
/// Grooming / training / boarding / walking marketplace.
///
/// Booking lifecycle (reuses AppointmentStatus):
///   PendingConfirmation → Confirmed → Completed
///                       ↘ CancelledByUser / CancelledByDoctor (= provider)
/// Payment is settled with the provider (cash / their terminal) and recorded
/// via MarkPaid; completed grooming bookings write a GroomingRecord to the pet.
/// </summary>
public class PetServicesMarketplace : IPetServicesMarketplace
{
    private static readonly AppointmentStatus[] LiveStatuses =
        { AppointmentStatus.PendingConfirmation, AppointmentStatus.Confirmed };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IKafkaProducer _kafka;
    private readonly IAuditLogger _audit;

    public PetServicesMarketplace(ApplicationDbContext db, ICurrentUserService current, IKafkaProducer kafka, IAuditLogger audit)
    {
        _db = db;
        _current = current;
        _kafka = kafka;
        _audit = audit;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();
    private bool Has(string permission) => _current.Permissions.Contains(permission);

    // ---------- Discovery ----------

    public async Task<PagedProviders> SearchAsync(ProviderQuery q, CancellationToken ct = default)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 50);
        var query = _db.ServiceProviders.AsNoTracking().Where(p => p.ApprovalStatus == ApprovalStatus.Approved);
        if (q.Type is { } t) query = query.Where(p => p.ProviderType == t);
        if (!string.IsNullOrWhiteSpace(q.City))
        {
            var city = $"%{q.City.Trim()}%";
            query = query.Where(p => p.City != null && EF.Functions.ILike(p.City, city));
        }
        if (q.AnimalType is { } a)
        {
            var name = a.ToString();
            query = query.Where(p => p.AnimalTypes == null || p.AnimalTypes.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = $"%{q.Q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.BusinessName, term) || (p.About != null && EF.Functions.ILike(p.About, term)));
        }
        query = q.Sort switch
        {
            "price_asc" => query.OrderBy(p => p.BasePrice),
            "price_desc" => query.OrderByDescending(p => p.BasePrice),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.RatingAverage).ThenByDescending(p => p.RatingCount)
        };

        var total = await query.CountAsync(ct);
        var rows = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedProviders(rows.Select(ToSummary).ToList(), total, page, size);
    }

    public async Task<ProviderDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.ServiceProviders.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        // Unapproved profiles are only visible to their owner and to approvers.
        if (p.ApprovalStatus != ApprovalStatus.Approved && p.UserId != _current.UserId && !Has(Permissions.Services.Approve))
            return null;
        return await ToDetailAsync(p, ct);
    }

    public async Task<ProviderDetailDto?> GetMineAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        var p = await _db.ServiceProviders.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == uid, ct);
        return p is null ? null : await ToDetailAsync(p, ct);
    }

    // ---------- Registration ----------

    public async Task<Guid> RegisterAsync(RegisterProviderInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        if (await _db.ServiceProviders.AnyAsync(p => p.UserId == uid, ct))
            throw new ConflictException("You already have a service provider profile.");

        var p = new ServiceProviderProfile { UserId = uid, ApprovalStatus = ApprovalStatus.Pending };
        Apply(p, input);
        _db.ServiceProviders.Add(p);
        _db.ApprovalRequests.Add(new ApprovalRequest
        {
            EntityType = ApprovalEntityType.ServiceProvider,
            EntityId = p.Id,
            SubmittedById = uid,
            SlaDueAt = DateTime.UtcNow.AddDays(2),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(input)
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("register", "ServiceProvider", p.Id.ToString(), "Services", newValues: input, ct: ct);
        return p.Id;
    }

    public async Task UpdateMineAsync(RegisterProviderInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var p = await _db.ServiceProviders.SingleOrDefaultAsync(x => x.UserId == uid, ct)
            ?? throw new NotFoundException("ServiceProvider", uid);
        Apply(p, input);
        // A rejected provider re-submits by editing their profile.
        if (p.ApprovalStatus == ApprovalStatus.Rejected)
        {
            p.ApprovalStatus = ApprovalStatus.Pending;
            _db.ApprovalRequests.Add(new ApprovalRequest
            {
                EntityType = ApprovalEntityType.ServiceProvider, EntityId = p.Id, SubmittedById = uid,
                SlaDueAt = DateTime.UtcNow.AddDays(2), PayloadJson = System.Text.Json.JsonSerializer.Serialize(input)
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(ServiceProviderProfile p, RegisterProviderInput i)
    {
        p.ProviderType = i.ProviderType;
        p.BusinessName = i.BusinessName.Trim();
        p.About = Clean(i.About);
        p.Address = Clean(i.Address);
        p.City = Clean(i.City);
        p.Country = Clean(i.Country);
        p.PhoneNumber = Clean(i.PhoneNumber);
        p.BasePrice = i.BasePrice;
        p.DefaultDurationMinutes = i.DefaultDurationMinutes;
        p.LogoUrl = Clean(i.LogoUrl);
        p.AnimalTypes = i.AnimalTypes is { Count: > 0 } ? string.Join(",", i.AnimalTypes.Distinct()) : null;
    }

    // ---------- Admin ----------

    public async Task<PagedProviders> AdminListAsync(ApprovalStatus status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Has(Permissions.Services.Approve)) throw new ForbiddenException();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.ServiceProviders.AsNoTracking().Where(p => p.ApprovalStatus == status).OrderBy(p => p.CreatedAt);
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedProviders(rows.Select(ToSummary).ToList(), total, page, pageSize);
    }

    public async Task DecideAsync(Guid id, ApprovalStatus status, string? notes, CancellationToken ct = default)
    {
        var required = status switch
        {
            ApprovalStatus.Approved => Permissions.Services.Approve,
            ApprovalStatus.Rejected => Permissions.Services.Reject,
            ApprovalStatus.Suspended => Permissions.Services.Suspend,
            _ => throw new ConflictException("Unsupported decision.")
        };
        if (!Has(required)) throw new ForbiddenException();
        var uid = Uid();

        var p = await _db.ServiceProviders.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("ServiceProvider", id);
        p.ApprovalStatus = status;
        p.AdminNotes = Clean(notes);
        if (status == ApprovalStatus.Approved) p.ApprovedAt = DateTime.UtcNow;

        var pending = await _db.ApprovalRequests.FirstOrDefaultAsync(a =>
            a.EntityType == ApprovalEntityType.ServiceProvider && a.EntityId == id && a.Decision == ApprovalDecision.Pending, ct);
        if (pending is not null)
        {
            pending.Decision = status == ApprovalStatus.Approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
            pending.DecidedById = uid;
            pending.DecidedAt = DateTime.UtcNow;
            pending.AdminNotes = Clean(notes);
        }

        _db.Notifications.Add(new InAppNotification
        {
            UserId = p.UserId,
            Title = status switch
            {
                ApprovalStatus.Approved => "Your service profile is live 🎉",
                ApprovalStatus.Rejected => "Your service profile was not approved",
                _ => "Your service profile has been suspended"
            },
            Body = Clean(notes) ?? "—",
            Url = "/services/dashboard"
        });
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(status.ToString().ToLowerInvariant(), "ServiceProvider", id.ToString(), "Services",
            newValues: new { notes }, ct: ct);
    }

    // ---------- Bookings ----------

    public async Task<BookingDto> BookAsync(CreateBookingInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var provider = await _db.ServiceProviders.SingleOrDefaultAsync(p => p.Id == input.ProviderId, ct)
            ?? throw new NotFoundException("ServiceProvider", input.ProviderId);
        if (provider.ApprovalStatus != ApprovalStatus.Approved) throw new ConflictException("This provider isn't accepting bookings.");
        if (provider.UserId == uid) throw new ConflictException("You can't book your own service.");

        if (input.PetId is { } pid)
        {
            var pet = await Pets.PetAccessService.AccessiblePets(_db, uid, Application.Modules.Pets.Dtos.PetAccessLevel.CoOwner)
                .Where(p => p.Id == pid).Select(p => new { p.AnimalType }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Pet", pid);
            if (provider.AnimalTypes is { } types && !types.Split(',').Contains(pet.AnimalType.ToString()))
                throw new ConflictException($"This provider doesn't serve {pet.AnimalType.ToString().ToLowerInvariant()}s.");
        }

        var start = Utc(input.ScheduledAt);
        var end = start.AddMinutes(provider.DefaultDurationMinutes);
        // Boarding runs in parallel (kennel capacity); other types are one-at-a-time.
        if (provider.ProviderType != ServiceProviderType.Boarding)
        {
            var clash = await _db.ServiceBookings.AnyAsync(b => b.ServiceProviderId == provider.Id
                && LiveStatuses.Contains(b.Status)
                && b.ScheduledAt < end && b.ScheduledAt.AddMinutes(b.DurationMinutes) > start, ct);
            if (clash) throw new ConflictException("That time is already booked — please pick another slot.");
        }

        var booking = new ServiceBooking
        {
            ServiceProviderId = provider.Id, UserId = uid, PetId = input.PetId, ScheduledAt = start,
            DurationMinutes = provider.DefaultDurationMinutes, Status = AppointmentStatus.PendingConfirmation,
            PaymentStatus = PaymentStatus.Unpaid, Amount = provider.BasePrice, Notes = Clean(input.Notes)
        };
        _db.ServiceBookings.Add(booking);
        _db.Notifications.Add(new InAppNotification
        {
            UserId = provider.UserId,
            Title = "New booking request",
            Body = $"{start:dd MMM yyyy HH:mm} UTC · confirm or decline it from your dashboard.",
            Url = "/services/dashboard"
        });
        await _db.SaveChangesAsync(ct);
        return (await ProjectBookings(_db.ServiceBookings.Where(b => b.Id == booking.Id)).SingleAsync(ct));
    }

    public async Task<IReadOnlyList<BookingDto>> MyBookingsAsync(CancellationToken ct = default)
    {
        var uid = Uid();
        return await ProjectBookings(_db.ServiceBookings.Where(b => b.UserId == uid).OrderByDescending(b => b.ScheduledAt))
            .Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BookingDto>> IncomingBookingsAsync(AppointmentStatus? status, CancellationToken ct = default)
    {
        var provider = await MyProviderAsync(ct);
        var q = _db.ServiceBookings.Where(b => b.ServiceProviderId == provider.Id);
        if (status is { } s) q = q.Where(b => b.Status == s);
        return await ProjectBookings(q.OrderBy(b => b.ScheduledAt)).Take(500).ToListAsync(ct);
    }

    public async Task ConfirmAsync(Guid bookingId, CancellationToken ct = default)
    {
        var (b, provider) = await ProviderBookingAsync(bookingId, ct);
        if (b.Status != AppointmentStatus.PendingConfirmation) throw new ConflictException("Only pending bookings can be confirmed.");
        b.Status = AppointmentStatus.Confirmed;
        b.ConfirmedAt = DateTime.UtcNow;
        Notify(b.UserId, $"{provider.BusinessName} confirmed your booking", $"See you on {b.ScheduledAt:dd MMM, HH:mm} UTC.");
        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid bookingId, CancellationToken ct = default)
    {
        var (b, provider) = await ProviderBookingAsync(bookingId, ct);
        if (b.Status != AppointmentStatus.Confirmed) throw new ConflictException("Only confirmed bookings can be completed.");
        b.Status = AppointmentStatus.Completed;
        b.CompletedAt = DateTime.UtcNow;

        if (b.PetId is { } pid && provider.ProviderType == ServiceProviderType.Grooming)
        {
            _db.GroomingRecords.Add(new GroomingRecord
            {
                PetId = pid, PerformedOn = DateTime.UtcNow, ServiceType = "Grooming",
                Provider = provider.BusinessName, Notes = b.Notes, Cost = b.Amount, ServiceBookingId = b.Id
            });
        }
        Notify(b.UserId, $"Your {provider.ProviderType.ToString().ToLowerInvariant()} session is complete",
            "Tap to leave a review.");
        await _db.SaveChangesAsync(ct);
        await _kafka.PublishAsync(PetTopics.Events,
            new ServiceBookingCompleted(b.Id, provider.Id, b.UserId, b.PetId, b.Amount, DateTime.UtcNow), b.Id.ToString(), ct);
    }

    public async Task MarkPaidAsync(Guid bookingId, CancellationToken ct = default)
    {
        var (b, _) = await ProviderBookingAsync(bookingId, ct);
        if (b.Status is AppointmentStatus.CancelledByUser or AppointmentStatus.CancelledByDoctor)
            throw new ConflictException("Cancelled bookings can't be marked paid.");
        b.PaymentStatus = PaymentStatus.Paid;
        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid bookingId, CancelBookingInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var b = await _db.ServiceBookings.Include(x => x.ServiceProvider).SingleOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        var byProvider = b.ServiceProvider.UserId == uid;
        if (!byProvider && b.UserId != uid) throw new ForbiddenException();
        if (!LiveStatuses.Contains(b.Status)) throw new ConflictException("This booking can no longer be cancelled.");
        if (!byProvider && b.ScheduledAt <= DateTime.UtcNow) throw new ConflictException("The booking has already started.");

        b.Status = byProvider ? AppointmentStatus.CancelledByDoctor : AppointmentStatus.CancelledByUser;
        b.CancellationReason = Clean(input.Reason);
        Notify(byProvider ? b.UserId : b.ServiceProvider.UserId,
            byProvider ? $"{b.ServiceProvider.BusinessName} cancelled your booking" : "A customer cancelled a booking",
            b.CancellationReason ?? $"Booking on {b.ScheduledAt:dd MMM, HH:mm} UTC");
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReviewAsync(Guid bookingId, ServiceReviewInput input, CancellationToken ct = default)
    {
        var uid = Uid();
        var b = await _db.ServiceBookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.UserId == uid, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        if (b.Status != AppointmentStatus.Completed) throw new ConflictException("You can review a booking once it's completed.");
        if (await _db.ServiceReviews.AnyAsync(r => r.BookingId == bookingId, ct)) throw new ConflictException("Already reviewed.");

        _db.ServiceReviews.Add(new ServiceReview
        {
            ServiceProviderId = b.ServiceProviderId, UserId = uid, BookingId = bookingId,
            Rating = input.Rating, Comment = Clean(input.Comment)
        });
        await _db.SaveChangesAsync(ct);

        var provider = await _db.ServiceProviders.SingleAsync(p => p.Id == b.ServiceProviderId, ct);
        var stats = await _db.ServiceReviews.Where(r => r.ServiceProviderId == provider.Id)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Avg = g.Average(r => (decimal)r.Rating) }).SingleAsync(ct);
        provider.RatingCount = stats.Count;
        provider.RatingAverage = Math.Round(stats.Avg, 2);
        await _db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private async Task<ServiceProviderProfile> MyProviderAsync(CancellationToken ct)
    {
        var uid = Uid();
        return await _db.ServiceProviders.SingleOrDefaultAsync(p => p.UserId == uid, ct)
            ?? throw new NotFoundException("ServiceProvider", uid);
    }

    private async Task<(ServiceBooking Booking, ServiceProviderProfile Provider)> ProviderBookingAsync(Guid bookingId, CancellationToken ct)
    {
        var provider = await MyProviderAsync(ct);
        var b = await _db.ServiceBookings.SingleOrDefaultAsync(x => x.Id == bookingId && x.ServiceProviderId == provider.Id, ct)
            ?? throw new NotFoundException("Booking", bookingId);
        return (b, provider);
    }

    private void Notify(Guid userId, string title, string body) =>
        _db.Notifications.Add(new InAppNotification { UserId = userId, Title = title, Body = body, Url = "/services/bookings" });

    private IQueryable<BookingDto> ProjectBookings(IQueryable<ServiceBooking> q) =>
        q.AsNoTracking().Select(b => new BookingDto(
            b.Id, b.ServiceProviderId, b.ServiceProvider.BusinessName, b.ServiceProvider.ProviderType, b.UserId,
            b.User.DisplayName, b.PetId, b.Pet != null ? b.Pet.Name : null, b.ScheduledAt, b.DurationMinutes, b.Status,
            b.PaymentStatus, b.Amount, b.Notes, b.CancellationReason,
            _db.ServiceReviews.Any(r => r.BookingId == b.Id), b.CreatedAt));

    private static ProviderSummaryDto ToSummary(ServiceProviderProfile p) => new(
        p.Id, p.UserId, p.BusinessName, p.ProviderType, p.About, p.City, p.Country, p.BasePrice, p.DefaultDurationMinutes,
        p.RatingAverage, p.RatingCount, p.LogoUrl,
        (p.AnimalTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<AnimalType>(s, out var a) ? (AnimalType?)a : null)
            .Where(a => a.HasValue).Select(a => a!.Value).ToList(),
        p.ApprovalStatus);

    private async Task<ProviderDetailDto> ToDetailAsync(ServiceProviderProfile p, CancellationToken ct)
    {
        var reviews = await _db.ServiceReviews.AsNoTracking().Where(r => r.ServiceProviderId == p.Id)
            .OrderByDescending(r => r.CreatedAt).Take(30)
            .Select(r => new ServiceReviewDto(r.Id, r.Rating, r.Comment, r.User.DisplayName, r.CreatedAt)).ToListAsync(ct);
        var completed = await _db.ServiceBookings.CountAsync(b => b.ServiceProviderId == p.Id && b.Status == AppointmentStatus.Completed, ct);
        var isPrivileged = p.UserId == _current.UserId || Has(Permissions.Services.Approve);
        return new ProviderDetailDto(ToSummary(p), p.Address, p.PhoneNumber, p.User.DisplayName, completed,
            isPrivileged ? p.AdminNotes : null, reviews);
    }
}
