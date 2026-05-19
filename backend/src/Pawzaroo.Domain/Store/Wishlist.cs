using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

/// <summary>
/// Per-user product save-for-later. Composite-unique on (UserId, ProductId)
/// so "add to wishlist" is idempotent.
/// </summary>
public class WishlistItem : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
}
