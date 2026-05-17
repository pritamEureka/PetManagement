using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Store;

public enum CartStatus { Active = 0, Abandoned = 1, Converted = 2 }

/// <summary>Cart header. CartItem keeps a denormalized UserId for fast cart-by-user lookups.</summary>
public class Cart : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public CartStatus Status { get; set; } = CartStatus.Active;
    public string Currency { get; set; } = "USD";
    public DateTime? AbandonedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
