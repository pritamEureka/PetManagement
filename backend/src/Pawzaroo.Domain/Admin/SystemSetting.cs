using Pawzaroo.Domain.Common;

namespace Pawzaroo.Domain.Admin;

/// <summary>
/// Key/value config store. ValueJson is JSONB — store anything: feature flags,
/// commission rates, SMTP creds (encrypted), pricing matrices, etc.
/// </summary>
public class SystemSetting : AuditableEntity
{
    public string Key { get; set; } = default!;
    public string Category { get; set; } = "general";
    public string ValueJson { get; set; } = "{}";
    public string? Description { get; set; }
    public bool IsSecret { get; set; }
}
