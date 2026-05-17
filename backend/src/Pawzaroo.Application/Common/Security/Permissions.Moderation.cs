namespace Pawzaroo.Application.Common.Security;

/// <summary>
/// Moderation-specific permission codes. These are *added* to the canonical
/// catalogue in <see cref="Pawzaroo.Application.Common.Permissions.Permissions"/>
/// — duplicated here so consumers can `using static` them in moderation services.
/// </summary>
public static class ModerationPermissions
{
    public const string ViewReports       = "moderation.view";
    public const string TakeAction        = "moderation.moderate";
    public const string ApproveContent    = "moderation.approve";
    public const string RejectContent     = "moderation.reject";
    public const string EscalateToSuper   = "moderation.escalate";

    public const string SuspendUser       = "users.suspend";
    public const string RestoreUser       = "users.restore";
    public const string WarnUser          = "users.warn";
    public const string BanUser           = "users.ban";
}
