using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Messaging;

public class ConversationPin : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public Guid PinnedBy { get; set; }
    public DateTime PinnedAt { get; set; } = DateTime.UtcNow;
}

public class ConversationFolder : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ConversationFolderItem : BaseEntity
{
    public Guid FolderId { get; set; }
    public ConversationFolder Folder { get; set; } = default!;
    public Guid ConversationId { get; set; }
}

public class ConversationSettings : BaseEntity
{
    public Guid ConversationId { get; set; }
    public int? DisappearAfterSeconds { get; set; }
    public DateTime? MutedUntil { get; set; }
    /// <summary>Opt-in E2EE: content is a client-produced ciphertext envelope, never decrypted server-side.</summary>
    public bool E2eeEnabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class MessageEdit : BaseEntity
{
    public Guid MessageId { get; set; }
    public string? PreviousContent { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}

public class UserE2eeKey : BaseEntity
{
    public Guid UserId { get; set; }
    public string PublicKeyJwk { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
