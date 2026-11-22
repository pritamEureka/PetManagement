using System.Text.RegularExpressions;

namespace Pawzaroo.Infrastructure.Modules.Messaging;

/// <summary>Edit / recall / expiry windows (pure).</summary>
public static class MessageWindows
{
    public static readonly TimeSpan EditWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RecallWindow = TimeSpan.FromMinutes(10);
    public const int MaxPins = 5;
    public static readonly int[] AllowedDisappearSeconds = { 3600, 86400, 604800, 2592000 };

    public static bool CanEdit(DateTime createdAtUtc, DateTime nowUtc) => nowUtc - createdAtUtc <= EditWindow && nowUtc >= createdAtUtc;
    public static bool CanRecall(DateTime createdAtUtc, DateTime nowUtc) => nowUtc - createdAtUtc <= RecallWindow && nowUtc >= createdAtUtc;

    public static bool IsValidDisappear(int? seconds) => seconds is null || AllowedDisappearSeconds.Contains(seconds.Value);

    public static bool IsExpired(DateTime createdAtUtc, int? ttlSeconds, DateTime nowUtc)
        => ttlSeconds is > 0 && nowUtc >= createdAtUtc.AddSeconds(ttlSeconds.Value);
}

public enum SpamVerdict { Ok, Flag, Block }

public record SpamAssessment(SpamVerdict Verdict, string? Reason);

/// <summary>Spam heuristics (pure). Counts are supplied by the caller from recent history.</summary>
public static class SpamHeuristics
{
    public const int DuplicateBlockThreshold = 4;   // the 5th identical message in the window is blocked
    public const int DuplicateFlagThreshold = 2;    // third identical message is flagged
    public const int LinkFlagThreshold = 3;
    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);
    private static readonly Regex LinkRx = new(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int CountLinks(string? content) => string.IsNullOrEmpty(content) ? 0 : LinkRx.Matches(content).Count;

    public static string Normalize(string? content) => Regex.Replace((content ?? "").Trim().ToLowerInvariant(), @"\s+", " ");

    public static SpamAssessment Assess(string? content, int identicalRecentCount, bool senderIsNew)
    {
        if (string.IsNullOrWhiteSpace(content)) return new(SpamVerdict.Ok, null);
        if (identicalRecentCount >= DuplicateBlockThreshold)
            return new(SpamVerdict.Block, "You are sending the same message repeatedly.");
        var links = CountLinks(content);
        if (links >= LinkFlagThreshold || (senderIsNew && links >= 2))
            return new(SpamVerdict.Flag, $"Link-heavy message ({links} links)");
        if (identicalRecentCount >= DuplicateFlagThreshold)
            return new(SpamVerdict.Flag, "Repeated identical message");
        return new(SpamVerdict.Ok, null);
    }
}

/// <summary>Coalescing of unread-message notifications per conversation (pure).</summary>
public static class NotificationCoalescing
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public static bool ShouldCoalesce(DateTime existingCreatedAtUtc, bool existingIsRead, DateTime nowUtc)
        => !existingIsRead && nowUtc - existingCreatedAtUtc <= Window;

    public static string Body(int count, string latestPreview)
        => count <= 1 ? latestPreview : $"{count} new messages. Latest: {latestPreview}";

    /// <summary>Parses the count out of a previously built coalesced body; 1 when not coalesced.</summary>
    public static int ParseCount(string body)
    {
        var m = Regex.Match(body, @"^(\d+) new messages");
        return m.Success ? int.Parse(m.Groups[1].Value) : 1;
    }
}
