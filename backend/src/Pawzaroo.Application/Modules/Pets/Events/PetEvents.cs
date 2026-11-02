using Pawzaroo.Application.Common.Messaging;

namespace Pawzaroo.Application.Modules.Pets.Events;

public static class PetTopics
{
    public const string Events = KafkaTopics.PetEvents;
}

public record PetCreated(Guid PetId, Guid OwnerId, string AnimalType, DateTime OccurredAt);
public record PetDeleted(Guid PetId, Guid OwnerId, DateTime OccurredAt);
public record PetMedicalRecordAdded(Guid PetId, Guid RecordId, Guid? AppointmentId, DateTime OccurredAt);
public record PetVaccinationRecorded(Guid PetId, Guid RecordId, string VaccineName, DateTime? NextDueOn, DateTime OccurredAt);
public record VaccinationDue(Guid PetId, Guid RecordId, Guid OwnerId, DateTime DueOn, int DaysBefore, DateTime OccurredAt);
public record PetOwnershipTransferred(Guid PetId, Guid FromUserId, Guid ToUserId, Guid? AdoptionListingId, DateTime OccurredAt);
public record PetReportedLost(Guid ReportId, Guid? PetId, Guid ReporterId, string City, DateTime OccurredAt);
public record PetFound(Guid ReportId, Guid ReporterId, string City, DateTime OccurredAt);
public record LostFoundResolved(Guid ReportId, bool Reunited, DateTime OccurredAt);
public record PetSightingReported(Guid PetId, Guid OwnerId, double? Latitude, double? Longitude, DateTime OccurredAt);
public record ServiceBookingCompleted(Guid BookingId, Guid ProviderId, Guid UserId, Guid? PetId, decimal Amount, DateTime OccurredAt);
