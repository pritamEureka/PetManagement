using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Pets;
using Pawzaroo.Domain.Social;
using Pawzaroo.Domain.Adoption;
using Pawzaroo.Domain.Messaging;
using Pawzaroo.Domain.Store;
using Pawzaroo.Domain.Veterinary;

namespace Pawzaroo.Domain.Identity;

public class User : AuditableEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? UserName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSuspended { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<AdoptionListing> AdoptionListings { get; set; } = new List<AdoptionListing>();
    public ICollection<ConversationParticipant> Conversations { get; set; } = new List<ConversationParticipant>();
    public Doctor? DoctorProfile { get; set; }
    public Store.Store? StoreProfile { get; set; }
}
