using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// Store-level rating (separate from <see cref="ProductReview"/>). Anchored to an
/// order so anonymous review spam is harder; one review per (UserId, OrderId, StoreId).
/// </summary>
public class StoreReview : AuditableEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public int Rating { get; set; }   // 1..5
    public string? Comment { get; set; }
    public bool IsVerifiedPurchase { get; set; }
}
