using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Application.Modules.Pets.Dtos;

public record LostFoundExtraInput(
    string? Markings, string? CollarDescription, string? Sex, string? AgeEstimate, string? MicrochipNumber,
    DateTime? TimeWindowStart, DateTime? TimeWindowEnd);

public record LostFoundExtraDto(
    string? Markings, string? CollarDescription, string? Sex, string? AgeEstimate,
    DateTime? TimeWindowStart, DateTime? TimeWindowEnd);

public record CreateSightingInput(DateTime SeenAt, double? Latitude, double? Longitude, string Description, string? PhotoUrl);

public record SightingDto(
    Guid Id, Guid ReportId, Guid ReporterUserId, string ReporterName, DateTime SeenAt, double? Latitude, double? Longitude,
    string Description, string? PhotoUrl, SightingStatus Status, DateTime? VerifiedAt, DateTime CreatedAt, bool IsMine);

public record AddEvidenceInput(string Url, EvidenceKind Kind);

public record EvidenceDto(Guid Id, Guid ReportId, string Url, EvidenceKind Kind, Guid UploadedBy, DateTime CreatedAt);

public record TimelineEventDto(string Kind, DateTime At, string Title, string? Detail, Guid? RefId);

public record AlertSubscriptionInput(double Latitude, double Longitude, double RadiusKm, AnimalType? AnimalType);

public record AlertSubscriptionDto(Guid Id, double Latitude, double Longitude, double RadiusKm, AnimalType? AnimalType, bool Active, DateTime CreatedAt);

public interface ILostFoundPlusService
{
    Task<SightingDto> AddSightingAsync(Guid reportId, CreateSightingInput input, CancellationToken ct = default);
    Task<IReadOnlyList<SightingDto>> ListSightingsAsync(Guid reportId, CancellationToken ct = default);
    /// <summary>Report owner (or moderator) verifies/rejects a sighting.</summary>
    Task<SightingDto> ReviewSightingAsync(Guid reportId, Guid sightingId, bool verified, CancellationToken ct = default);
    Task<IReadOnlyList<TimelineEventDto>> TimelineAsync(Guid reportId, CancellationToken ct = default);
    Task<EvidenceDto> AddEvidenceAsync(Guid reportId, AddEvidenceInput input, CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceDto>> ListEvidenceAsync(Guid reportId, CancellationToken ct = default);

    Task<IReadOnlyList<AlertSubscriptionDto>> ListSubscriptionsAsync(CancellationToken ct = default);
    Task<AlertSubscriptionDto> SubscribeAsync(AlertSubscriptionInput input, CancellationToken ct = default);
    Task UnsubscribeAsync(Guid id, CancellationToken ct = default);
}
