using Pawzaroo.Domain.Common;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Domain.Notifications;

public class InAppNotification : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string? Payload { get; set; }
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
