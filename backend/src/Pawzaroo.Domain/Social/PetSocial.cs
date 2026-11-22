using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Social;

public enum AlbumVisibility { Public = 0, Followers = 1, Private = 2 }

public class PetAlbum : AuditableEntity
{
    public Guid PetId { get; set; }
    public string Title { get; set; } = default!;
    public AlbumVisibility Visibility { get; set; } = AlbumVisibility.Public;
    public string? CoverUrl { get; set; }
    public ICollection<PetAlbumPhoto> Photos { get; set; } = new List<PetAlbumPhoto>();
}

public class PetAlbumPhoto : BaseEntity
{
    public Guid AlbumId { get; set; }
    public PetAlbum Album { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Caption { get; set; }
    public DateTime? TakenAt { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>User follows a pet profile. (UserId, PetId) unique; hard-deleted on unfollow.</summary>
public class PetFollow : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Optional geo-tag for a post (side table so the hot posts table is untouched).</summary>
public class PostLocation : BaseEntity
{
    public Guid PostId { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
