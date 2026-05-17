using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Audit;

public class AuditEntry : BaseEntity
{
    public DateTime At { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;          // create | update | delete | login | approve | ...
    public string EntityName { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? Module { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}
