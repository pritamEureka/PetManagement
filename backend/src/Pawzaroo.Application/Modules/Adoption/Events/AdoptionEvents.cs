namespace Pawzaroo.Application.Modules.Adoption.Events;

public static class AdoptionTopics
{
    public const string Events    = "pawzaroo.adoption.events";
    public const string Approvals = "pawzaroo.adoption.approvals";
}

public record AdoptionListingCreated(Guid ListingId, Guid OwnerId, bool DraftSubmitted, DateTime At);
public record AdoptionListingSubmitted(Guid ListingId, Guid OwnerId, DateTime At);
public record AdoptionListingUpdated(Guid ListingId, Guid OwnerId, DateTime At);
public record AdoptionListingDeleted(Guid ListingId, Guid OwnerId, DateTime At);
public record AdoptionListingApproved(Guid ListingId, Guid ApprovedBy, DateTime At);
public record AdoptionListingRejected(Guid ListingId, Guid RejectedBy, string? Reason, DateTime At);
public record AdoptionListingAdopted(Guid ListingId, Guid OwnerId, Guid? AdoptedByUserId, DateTime At);
public record AdoptionListingClosed(Guid ListingId, Guid OwnerId, DateTime At);

public record AdoptionListingSaved(Guid ListingId, Guid UserId, bool Saved, DateTime At);

public record AdoptionRequestCreated(Guid RequestId, Guid ListingId, Guid RequesterId, DateTime At);
public record AdoptionRequestStatusChanged(Guid RequestId, Guid ListingId, string Status, DateTime At);

public record AdoptionWantedPostCreated(Guid PostId, Guid RequesterId, DateTime At);
