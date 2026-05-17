using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Domain.Services;

public class ServiceProviderProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public ServiceProviderType ProviderType { get; set; }
    public string BusinessName { get; set; } = default!;
    public string? About { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal BasePrice { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;
    public decimal RatingAverage { get; set; }
    public int RatingCount { get; set; }

    public ICollection<ServiceBooking> Bookings { get; set; } = new List<ServiceBooking>();
}

public class ServiceBooking : AuditableEntity
{
    public Guid ServiceProviderId { get; set; }
    public ServiceProviderProfile ServiceProvider { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid? PetId { get; set; }
    public Pet? Pet { get; set; }
    public DateTime ScheduledAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class ServiceReview : AuditableEntity
{
    public Guid ServiceProviderId { get; set; }
    public ServiceProviderProfile ServiceProvider { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid BookingId { get; set; }
    public ServiceBooking Booking { get; set; } = default!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
