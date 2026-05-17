using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Domain.Social;

public class Post : AuditableEntity
{
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = default!;
    public string? Content { get; set; }
    public AnimalType? AnimalType { get; set; }
    public string? Location { get; set; }
    public bool IsHidden { get; set; }

    public ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
    public ICollection<PostHashtag> Hashtags { get; set; } = new List<PostHashtag>();
    public ICollection<PostPetTag> PetTags { get; set; } = new List<PostPetTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    public ICollection<PostShare> Shares { get; set; } = new List<PostShare>();
    public ICollection<PostSave> Saves { get; set; } = new List<PostSave>();
    public ICollection<PostReport> Reports { get; set; } = new List<PostReport>();
}

public class PostMedia : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string MediaType { get; set; } = "image";
    public int OrderIndex { get; set; }
}

public class Hashtag : BaseEntity
{
    public string Tag { get; set; } = default!;
    public ICollection<PostHashtag> PostHashtags { get; set; } = new List<PostHashtag>();
}

public class PostHashtag
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid HashtagId { get; set; }
    public Hashtag Hashtag { get; set; } = default!;
}

public class PostPetTag
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid PetId { get; set; }
    public Pet Pet { get; set; } = default!;
}

public class Comment : AuditableEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = default!;
    public Guid? ParentCommentId { get; set; }
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public string Content { get; set; } = default!;
}

public class Reaction : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public ReactionType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostShare : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostSave : BaseEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PostReport : AuditableEntity
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = default!;
    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public string? Details { get; set; }
    public bool Resolved { get; set; }
}
