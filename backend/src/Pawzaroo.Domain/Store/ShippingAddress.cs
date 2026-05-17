using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// Saved shipping address. Order copies a snapshot at checkout — these rows are
/// the *address book*, not the historical record on the order itself.
/// </summary>
public class ShippingAddress : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string Label { get; set; } = "Home";
    public string RecipientName { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string AddressLine1 { get; set; } = default!;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }
}
