using FluentValidation;
using Pawzaroo.Domain.Common;

namespace Pawzaroo.Application.Modules.PetServices;

// ---------- DTOs ----------

public record ProviderQuery(ServiceProviderType? Type, string? City, AnimalType? AnimalType, string? Q,
    string? Sort, int Page = 1, int PageSize = 20);

public record ProviderSummaryDto(
    Guid Id, Guid UserId, string BusinessName, ServiceProviderType ProviderType, string? About, string? City,
    string? Country, decimal BasePrice, int DefaultDurationMinutes, decimal RatingAverage, int RatingCount,
    string? LogoUrl, IReadOnlyList<AnimalType> AnimalTypes, ApprovalStatus ApprovalStatus);

public record ServiceReviewDto(Guid Id, int Rating, string? Comment, string UserName, DateTime CreatedAt);

public record ProviderDetailDto(ProviderSummaryDto Provider, string? Address, string? PhoneNumber, string OwnerName,
    int CompletedBookings, string? AdminNotes, IReadOnlyList<ServiceReviewDto> Reviews);

public record PagedProviders(IReadOnlyList<ProviderSummaryDto> Items, int Total, int Page, int PageSize);

public record RegisterProviderInput(
    ServiceProviderType ProviderType, string BusinessName, string? About, string? Address, string? City,
    string? Country, string? PhoneNumber, decimal BasePrice, int DefaultDurationMinutes, string? LogoUrl,
    IReadOnlyList<AnimalType>? AnimalTypes);

public record CreateBookingInput(Guid ProviderId, Guid? PetId, DateTime ScheduledAt, string? Notes);
public record CancelBookingInput(string? Reason);
public record ServiceReviewInput(int Rating, string? Comment);
public record ProviderDecisionInput(string? Notes);

public record BookingDto(
    Guid Id, Guid ProviderId, string ProviderName, ServiceProviderType ProviderType, Guid UserId, string CustomerName,
    Guid? PetId, string? PetName, DateTime ScheduledAt, int DurationMinutes, AppointmentStatus Status,
    PaymentStatus PaymentStatus, decimal Amount, string? Notes, string? CancellationReason, bool HasReview, DateTime CreatedAt);

// ---------- Service ----------

public interface IPetServicesMarketplace
{
    Task<PagedProviders> SearchAsync(ProviderQuery query, CancellationToken ct = default);
    Task<ProviderDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProviderDetailDto?> GetMineAsync(CancellationToken ct = default);
    Task<Guid> RegisterAsync(RegisterProviderInput input, CancellationToken ct = default);
    Task UpdateMineAsync(RegisterProviderInput input, CancellationToken ct = default);

    // Admin
    Task<PagedProviders> AdminListAsync(ApprovalStatus status, int page, int pageSize, CancellationToken ct = default);
    Task DecideAsync(Guid id, ApprovalStatus status, string? notes, CancellationToken ct = default);

    // Bookings
    Task<BookingDto> BookAsync(CreateBookingInput input, CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> MyBookingsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BookingDto>> IncomingBookingsAsync(AppointmentStatus? status, CancellationToken ct = default);
    Task ConfirmAsync(Guid bookingId, CancellationToken ct = default);
    Task CompleteAsync(Guid bookingId, CancellationToken ct = default);
    Task MarkPaidAsync(Guid bookingId, CancellationToken ct = default);
    Task CancelAsync(Guid bookingId, CancelBookingInput input, CancellationToken ct = default);
    Task ReviewAsync(Guid bookingId, ServiceReviewInput input, CancellationToken ct = default);
}

// ---------- Validators ----------

public class RegisterProviderInputValidator : AbstractValidator<RegisterProviderInput>
{
    public RegisterProviderInputValidator()
    {
        RuleFor(x => x.ProviderType).IsInEnum();
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.About).MaximumLength(2000);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.City).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Country).MaximumLength(128);
        RuleFor(x => x.PhoneNumber).MaximumLength(32);
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0).LessThan(1_000_000);
        RuleFor(x => x.DefaultDurationMinutes).InclusiveBetween(15, 24 * 60);
        RuleFor(x => x.LogoUrl).Must(u => string.IsNullOrWhiteSpace(u) || Uri.TryCreate(u, UriKind.Absolute, out _));
    }
}

public class CreateBookingInputValidator : AbstractValidator<CreateBookingInput>
{
    public CreateBookingInputValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.ScheduledAt).GreaterThan(_ => DateTime.UtcNow.AddMinutes(30))
            .WithMessage("Bookings must be at least 30 minutes in the future.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class ServiceReviewInputValidator : AbstractValidator<ServiceReviewInput>
{
    public ServiceReviewInputValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}
