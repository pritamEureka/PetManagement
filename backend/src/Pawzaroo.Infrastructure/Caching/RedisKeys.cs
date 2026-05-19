namespace Pawzaroo.Infrastructure.Caching;

/// <summary>
/// Single source of truth for every Redis key the app touches.
///
/// Why centralize: typos lead to silent cache misses (worse: poisoned caches
/// when reader and writer disagree on the key). Every key has a stable prefix
/// in the form <c>{module}:{purpose}:{...id}</c>; namespace bumps (versioned)
/// are appended as <c>:v{n}</c> on the suffix when the value shape changes.
///
/// All TTLs live here too — easier to audit cache pressure in one place.
/// </summary>
public static class RedisKeys
{
    // ---- Top-level prefixes -------------------------------------------------
    public const string AuthSession      = "auth:session";        // refresh-token jti -> userId
    public const string AuthBlacklist    = "auth:blacklist";      // jti -> "1" (revoked access tokens)
    public const string AuthOtp          = "auth:otp";            // email|phone -> otp
    public const string AuthOtpAttempts  = "auth:otp:attempts";   // email|phone -> count
    public const string Rbac             = "rbac:perms";          // userId -> JSON string[]
    public const string Rate             = "rate";                // {scope}:{partition} -> count (token bucket)
    public const string Feed             = "feed";                // feed module
    public const string Marketplace      = "marketplace";         // marketplace module
    public const string Vet              = "vet";                 // doctors, availability, slot locks
    public const string Notify           = "notify";              // notification counters + last fetch
    public const string Presence         = "presence";            // user/connection presence
    public const string Inbox            = "inbox";               // event idempotency cache
    public const string Outbox           = "outbox";              // outbox worker leader-lock

    // ---- Compose ------------------------------------------------------------
    public static string Join(params object[] parts) => string.Join(':', parts);

    // ---- Authentication / sessions -----------------------------------------
    public static string Session(string jti)        => $"{AuthSession}:{jti}";
    public static string Blacklist(string jti)      => $"{AuthBlacklist}:{jti}";
    public static string Otp(string subject)        => $"{AuthOtp}:{subject}";
    public static string OtpAttempts(string subject) => $"{AuthOtpAttempts}:{subject}";

    // ---- RBAC ---------------------------------------------------------------
    public static string UserPermissions(Guid userId) => $"{Rbac}:{userId}";

    // ---- Rate limiting (sliding/fixed windows owned by RedisRateLimiter) ----
    public static string Rl(string scope, string partition) => $"{Rate}:{scope}:{partition}";

    // ---- Feed ---------------------------------------------------------------
    public static string FeedFirstPage(string scope)    => $"{Feed}:first:{scope}";
    public static string FeedReactionCount(Guid postId) => $"{Feed}:reaction_count:{postId}";
    public static string FeedCommentCount(Guid postId)  => $"{Feed}:comment_count:{postId}";
    public static string FeedUserCursor(Guid uid, string scope) => $"{Feed}:cursor:{uid}:{scope}";

    // ---- Marketplace --------------------------------------------------------
    public static string MarketCategories()                  => $"{Marketplace}:categories:v1";
    public static string MarketProductFirstPage(long ver, string variant) => $"{Marketplace}:products:first:{ver}:{variant}";
    public static string MarketProductVersion()              => $"{Marketplace}:products:version";
    public static string MarketStore(Guid storeId)           => $"{Marketplace}:store:{storeId}";

    // ---- Vet ----------------------------------------------------------------
    public static string DoctorAvailability(Guid doctorId, DateOnly date) => $"{Vet}:availability:{doctorId}:{date:yyyyMMdd}";
    public static string SlotLock(Guid doctorId, Guid slotId)             => $"lock:slot:{doctorId}:{slotId}";

    // ---- Notifications ------------------------------------------------------
    public static string NotifyUnread(Guid userId)              => $"{Notify}:unread:{userId}";
    public static string NotifyLast(Guid userId)                => $"{Notify}:last:{userId}";
    public static string NotifyProducerRl(Guid userId, string h) => $"{Notify}:rl:{userId}:{h}";
    public static string NotifyConsumerSeen(string scope, Guid notificationId)
        => $"{Notify}:seen:{scope}:{notificationId}";

    // ---- Messaging ----------------------------------------------------------
    public static string MsgUnread(Guid userId)                          => $"msg:unread:{userId}";
    public static string MsgUnreadConvo(Guid userId, Guid conversationId) => $"msg:unread:{userId}:{conversationId}";

    // ---- Presence -----------------------------------------------------------
    public static string PresenceConnections(Guid userId) => $"{Presence}:user:{userId}";
    public static string PresenceLastSeen(Guid userId)    => $"{Presence}:last:{userId}";

    // ---- Consumer idempotency ----------------------------------------------
    public static string Inboxed(string consumerGroup, Guid eventId)
        => $"{Inbox}:{consumerGroup}:{eventId}";

    // ---- Outbox processing leader ------------------------------------------
    public static string OutboxLeader() => $"{Outbox}:leader";
}

/// <summary>Default TTLs in one place — change here, hits everywhere.</summary>
public static class RedisTtls
{
    public static readonly TimeSpan Session            = TimeSpan.FromDays(14);
    public static readonly TimeSpan Blacklist          = TimeSpan.FromHours(2);   // matches access-token TTL
    public static readonly TimeSpan Otp                = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan OtpAttempts        = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan UserPermissions    = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan FeedFirstPage      = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan FeedUserCursor     = TimeSpan.FromHours(1);
    public static readonly TimeSpan MarketCategories   = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan MarketStore        = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MarketProductPage  = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan DoctorAvailability = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan NotifyUnread       = TimeSpan.FromDays(7);
    public static readonly TimeSpan NotifyProducerRl   = TimeSpan.FromMinutes(1);  // window for per-user producer throttle
    public static readonly TimeSpan NotifyConsumerSeen = TimeSpan.FromHours(1);    // dedup window for re-delivered events
    public static readonly TimeSpan Inboxed            = TimeSpan.FromDays(3);
    public static readonly TimeSpan OutboxLeader       = TimeSpan.FromSeconds(15);
}
