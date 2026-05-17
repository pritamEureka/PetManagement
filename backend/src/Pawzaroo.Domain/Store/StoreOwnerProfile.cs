using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// One-row-per-user store-owner KYC profile. Decoupled from <see cref="Store"/>
/// so a user can submit/refresh KYC before (or independently of) opening a store.
/// </summary>
public class StoreOwnerProfile : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string LegalName { get; set; } = default!;
    public string? BusinessName { get; set; }
    public string? TradeLicenseNumber { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? TaxId { get; set; }

    public string? TradeLicenseDocUrl { get; set; }
    public string? NationalIdDocUrl { get; set; }
    public string? AddressProofDocUrl { get; set; }

    public ApprovalStatus KycStatus { get; set; } = ApprovalStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}
