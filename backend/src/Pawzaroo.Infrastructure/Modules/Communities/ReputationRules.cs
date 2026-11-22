using System.Security.Cryptography;
using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Infrastructure.Modules.Communities;

/// <summary>Pure reputation rules: points, levels, daily caps and idempotency keys.</summary>
public static class ReputationRules
{
    public const int DailyPositiveCap = 60;
    public static readonly int[] LevelThresholds = { 0, 10, 50, 150, 400, 1000 };
    public static readonly string[] LevelNames = { "Newcomer", "Regular", "Contributor", "Trusted", "Expert", "Legend" };

    public static int PointsFor(ReputationKind kind) => kind switch
    {
        ReputationKind.PostLiked => 1,
        ReputationKind.CommentLiked => 1,
        ReputationKind.PollCreated => 2,
        ReputationKind.EventHosted => 10,
        ReputationKind.EventAttended => 3,
        ReputationKind.HelpfulAnswer => 5,
        ReputationKind.ReportUpheld => 3,
        ReputationKind.Penalty => -10,
        _ => 0
    };

    /// <summary>Per-kind daily ceiling so one action type can't be farmed.</summary>
    public static int DailyKindCap(ReputationKind kind) => kind switch
    {
        ReputationKind.PostLiked or ReputationKind.CommentLiked => 20,
        ReputationKind.PollCreated => 6,
        ReputationKind.EventHosted => 20,
        ReputationKind.EventAttended => 9,
        ReputationKind.HelpfulAnswer => 25,
        ReputationKind.ReportUpheld => 9,
        _ => int.MaxValue
    };

    /// <summary>
    /// Whether an award of <paramref name="points"/> fits within the caps. Penalties are never capped.
    /// </summary>
    public static bool WithinCaps(ReputationKind kind, int points, int positiveToday, int kindToday)
    {
        if (points <= 0) return true;
        return positiveToday + points <= DailyPositiveCap && kindToday + points <= DailyKindCap(kind);
    }

    public static int LevelFor(int total)
    {
        var level = 0;
        for (var i = 0; i < LevelThresholds.Length; i++) if (total >= LevelThresholds[i]) level = i;
        return level;
    }

    public static string LevelName(int level) => LevelNames[Math.Clamp(level, 0, LevelNames.Length - 1)];

    /// <summary>Points needed for the next level (or the current total when already at the top).</summary>
    public static int NextLevelAt(int total)
    {
        var l = LevelFor(total);
        return l + 1 < LevelThresholds.Length ? LevelThresholds[l + 1] : total;
    }

    /// <summary>Deterministic source id for "A did X to B" style events (e.g. user likes post), stable across re-reactions.</summary>
    public static Guid SourceKey(Guid a, Guid b)
    {
        Span<byte> buf = stackalloc byte[32];
        a.TryWriteBytes(buf[..16]);
        b.TryWriteBytes(buf[16..]);
        var hash = SHA256.HashData(buf);
        return new Guid(hash.AsSpan(0, 16));
    }
}

public record TallyOption(Guid Id, int Votes);

/// <summary>Pure poll tally: percentages are of distinct voters (so multi-choice can exceed 100% in total).</summary>
public static class PollTally
{
    public static IReadOnlyDictionary<Guid, double> Percentages(IReadOnlyList<TallyOption> options, int voters)
    {
        var map = new Dictionary<Guid, double>();
        foreach (var o in options)
            map[o.Id] = voters <= 0 ? 0 : Math.Round(100.0 * Math.Min(o.Votes, voters) / voters, 1);
        return map;
    }

    /// <summary>Validates a ballot: option ids distinct, all belong to the poll, exactly one when single-choice.</summary>
    public static string? ValidateBallot(IReadOnlyCollection<Guid> pollOptionIds, IReadOnlyList<Guid> chosen, bool multiChoice)
    {
        if (chosen.Count == 0) return "Pick at least one option.";
        if (chosen.Distinct().Count() != chosen.Count) return "Duplicate options.";
        if (!multiChoice && chosen.Count != 1) return "This poll allows a single choice.";
        if (chosen.Any(c => !pollOptionIds.Contains(c))) return "Unknown option.";
        return null;
    }
}
