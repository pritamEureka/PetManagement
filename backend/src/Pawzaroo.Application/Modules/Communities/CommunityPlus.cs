using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Application.Modules.Communities;

public record CreatePollRequest(string Question, IReadOnlyList<string> Options, bool MultiChoice, DateTime? ClosesAt);

public record PollOptionResultDto(Guid Id, string Text, int Votes, double Percent, bool Voted);

public record PollDto(
    Guid Id, Guid CommunityId, string Question, bool MultiChoice, DateTime? ClosesAt, bool IsClosed,
    Guid CreatedByUserId, string CreatedByName, DateTime CreatedAt, int TotalVoters, bool HasVoted, bool CanManage,
    IReadOnlyList<PollOptionResultDto> Options);

public record PollVoteRequest(IReadOnlyList<Guid> OptionIds);

public record ReputationDto(Guid UserId, string DisplayName, string? AvatarUrl, int Total, int Level, string LevelName, int Rank);

public record MyReputationDto(int Total, int Level, string LevelName, int NextLevelAt);

public interface ICommunityPlusService
{
    Task<IReadOnlyList<PollDto>> ListPollsAsync(Guid communityId, CancellationToken ct = default);
    Task<PollDto> CreatePollAsync(Guid communityId, CreatePollRequest req, CancellationToken ct = default);
    Task<PollDto> VoteAsync(Guid pollId, PollVoteRequest req, CancellationToken ct = default);
    Task<PollDto> ClosePollAsync(Guid pollId, CancellationToken ct = default);
    Task DeletePollAsync(Guid pollId, CancellationToken ct = default);

    Task<IReadOnlyList<ReputationDto>> LeaderboardAsync(Guid communityId, int take, CancellationToken ct = default);
    /// <summary>Caller's reputation: community-scoped when <paramref name="communityId"/> is set, else global.</summary>
    Task<MyReputationDto> MyReputationAsync(Guid? communityId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, ReputationBadgeDto>> BadgesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct = default);
}

public record ReputationBadgeDto(int Total, int Level, string LevelName);

/// <summary>Thin award API called from existing actions. Idempotent and abuse-capped; never throws into the caller.</summary>
public interface IReputationService
{
    /// <returns>True when points were actually granted.</returns>
    Task<bool> AwardAsync(Guid userId, Guid? communityId, ReputationKind kind, Guid sourceId, CancellationToken ct = default);
}
