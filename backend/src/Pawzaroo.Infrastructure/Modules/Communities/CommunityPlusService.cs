using Microsoft.EntityFrameworkCore;
using Pawzaroo.Application.Common.Interfaces;
using Pawzaroo.Application.Modules.Communities;
using Pawzaroo.Domain.Communities;
using Pawzaroo.Domain.Identity;
using Pawzaroo.Infrastructure.Persistence;
using Pawzaroo.Shared.Exceptions;

namespace Pawzaroo.Infrastructure.Modules.Communities;

/// <summary>Community polls and the reputation leaderboard. Polls are member-only; moderators can close/delete.</summary>
public class CommunityPlusService : ICommunityPlusService
{
    private const int MaxOptions = 10;
    private const int MaxOpenPollsPerUser = 5;

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IReputationService _rep;

    public CommunityPlusService(ApplicationDbContext db, ICurrentUserService current, IReputationService rep)
    {
        _db = db;
        _current = current;
        _rep = rep;
    }

    private Guid Uid() => _current.UserId ?? throw new ForbiddenException();

    private static Exception Invalid(string field, string msg) =>
        new Shared.Exceptions.ValidationException(new Dictionary<string, string[]> { [field] = new[] { msg } });

    private static bool IsMod(CommunityMember? m) =>
        m is { Status: CommunityMemberStatus.Active, Role: CommunityRole.Moderator or CommunityRole.Owner };

    private Task<CommunityMember?> MemberAsync(Guid communityId, Guid uid, CancellationToken ct) =>
        _db.Set<CommunityMember>().AsNoTracking().FirstOrDefaultAsync(m => m.CommunityId == communityId && m.UserId == uid, ct);

    private async Task<CommunityMember?> RequireReadAsync(Guid communityId, CancellationToken ct)
    {
        var c = await _db.Set<Community>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == communityId, ct)
            ?? throw new NotFoundException("Community", communityId);
        var uid = _current.UserId;
        var m = uid is { } u ? await MemberAsync(communityId, u, ct) : null;
        if (!c.IsPublic && m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Members only.");
        return m;
    }

    // ---------------------------------------------------------------- polls

    public async Task<IReadOnlyList<PollDto>> ListPollsAsync(Guid communityId, CancellationToken ct = default)
    {
        var m = await RequireReadAsync(communityId, ct);
        var polls = await _db.Set<CommunityPoll>().AsNoTracking().Include(p => p.Options)
            .Where(p => p.CommunityId == communityId).OrderByDescending(p => p.CreatedAt).Take(50).ToListAsync(ct);
        return await ToDtosAsync(polls, IsMod(m), ct);
    }

    public async Task<PollDto> CreatePollAsync(Guid communityId, CreatePollRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var m = await MemberAsync(communityId, uid, ct);
        if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Only active members can create polls.");

        var q = req.Question?.Trim();
        if (string.IsNullOrEmpty(q) || q.Length > 300) throw Invalid("question", "Question is required (max 300 characters).");
        var options = (req.Options ?? Array.Empty<string>()).Select(o => o?.Trim() ?? "").Where(o => o.Length > 0).ToList();
        if (options.Count < 2 || options.Count > MaxOptions) throw Invalid("options", $"Provide 2 to {MaxOptions} options.");
        if (options.Any(o => o.Length > 160)) throw Invalid("options", "Options are limited to 160 characters.");
        if (options.Select(o => o.ToLowerInvariant()).Distinct().Count() != options.Count) throw Invalid("options", "Options must be unique.");
        DateTime? closes = req.ClosesAt is { } c
            ? (c.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(c, DateTimeKind.Utc) : c.ToUniversalTime())
            : null;
        if (closes is { } cl && cl <= DateTime.UtcNow.AddMinutes(1)) throw Invalid("closesAt", "Closing time must be in the future.");

        var now = DateTime.UtcNow;
        var open = await _db.Set<CommunityPoll>().CountAsync(p => p.CreatedByUserId == uid && p.ClosedAt == null
            && (p.ClosesAt == null || p.ClosesAt > now), ct);
        if (open >= MaxOpenPollsPerUser) throw new ConflictException("You have too many open polls. Close one first.");

        var poll = new CommunityPoll
        {
            CommunityId = communityId, CreatedByUserId = uid, Question = q, MultiChoice = req.MultiChoice, ClosesAt = closes
        };
        for (var i = 0; i < options.Count; i++) poll.Options.Add(new PollOption { Text = options[i], SortOrder = i });
        _db.Set<CommunityPoll>().Add(poll);
        await _db.SaveChangesAsync(ct);
        await _rep.AwardAsync(uid, communityId, ReputationKind.PollCreated, poll.Id, ct);
        return (await ToDtosAsync(new List<CommunityPoll> { poll }, IsMod(m), ct))[0];
    }

    public async Task<PollDto> VoteAsync(Guid pollId, PollVoteRequest req, CancellationToken ct = default)
    {
        var uid = Uid();
        var poll = await _db.Set<CommunityPoll>().Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw new NotFoundException("Poll", pollId);
        var m = await MemberAsync(poll.CommunityId, uid, ct);
        if (m is not { Status: CommunityMemberStatus.Active }) throw new ForbiddenException("Join the community to vote.");
        if (IsClosed(poll)) throw new ConflictException("This poll is closed.");

        var chosen = (req.OptionIds ?? Array.Empty<Guid>()).ToList();
        var err = PollTally.ValidateBallot(poll.Options.Select(o => o.Id).ToHashSet(), chosen, poll.MultiChoice);
        if (err is not null) throw Invalid("optionIds", err);

        // Changing a vote replaces the previous ballot for this poll (keeps the single-choice invariant).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Set<PollVote>().Where(v => v.PollId == pollId && v.UserId == uid).ExecuteDeleteAsync(ct);
        foreach (var o in chosen) _db.Set<PollVote>().Add(new PollVote { PollId = pollId, OptionId = o, UserId = uid });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (await ToDtosAsync(new List<CommunityPoll> { poll }, IsMod(m), ct))[0];
    }

    public async Task<PollDto> ClosePollAsync(Guid pollId, CancellationToken ct = default)
    {
        var uid = Uid();
        var poll = await _db.Set<CommunityPoll>().Include(p => p.Options).FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw new NotFoundException("Poll", pollId);
        var m = await MemberAsync(poll.CommunityId, uid, ct);
        if (!IsMod(m) && poll.CreatedByUserId != uid) throw new ForbiddenException();
        if (poll.ClosedAt is null) { poll.ClosedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
        return (await ToDtosAsync(new List<CommunityPoll> { poll }, true, ct))[0];
    }

    public async Task DeletePollAsync(Guid pollId, CancellationToken ct = default)
    {
        var uid = Uid();
        var poll = await _db.Set<CommunityPoll>().FirstOrDefaultAsync(p => p.Id == pollId, ct)
            ?? throw new NotFoundException("Poll", pollId);
        var m = await MemberAsync(poll.CommunityId, uid, ct);
        if (!IsMod(m) && poll.CreatedByUserId != uid) throw new ForbiddenException();
        _db.Set<CommunityPoll>().Remove(poll);   // soft delete via the auditable interceptor
        await _db.Set<PollVote>().Where(v => v.PollId == pollId).ExecuteDeleteAsync(ct);
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsClosed(CommunityPoll p) => p.ClosedAt != null || (p.ClosesAt != null && p.ClosesAt <= DateTime.UtcNow);

    private async Task<IReadOnlyList<PollDto>> ToDtosAsync(List<CommunityPoll> polls, bool viewerIsMod, CancellationToken ct)
    {
        var uid = _current.UserId;
        var ids = polls.Select(p => p.Id).ToList();
        var votes = await _db.Set<PollVote>().AsNoTracking().Where(v => ids.Contains(v.PollId))
            .Select(v => new { v.PollId, v.OptionId, v.UserId }).ToListAsync(ct);
        var creators = polls.Select(p => p.CreatedByUserId).Distinct().ToList();
        var names = await _db.Set<User>().AsNoTracking().Where(u => creators.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return polls.Select(p =>
        {
            var pv = votes.Where(v => v.PollId == p.Id).ToList();
            var voters = pv.Select(v => v.UserId).Distinct().Count();
            var mine = pv.Where(v => v.UserId == uid).Select(v => v.OptionId).ToHashSet();
            var counts = p.Options.OrderBy(o => o.SortOrder).Select(o => new TallyOption(o.Id, pv.Count(v => v.OptionId == o.Id))).ToList();
            var pct = PollTally.Percentages(counts, voters);
            var opts = p.Options.OrderBy(o => o.SortOrder)
                .Select(o => new PollOptionResultDto(o.Id, o.Text, counts.First(c => c.Id == o.Id).Votes, pct[o.Id], mine.Contains(o.Id)))
                .ToList();
            return new PollDto(p.Id, p.CommunityId, p.Question, p.MultiChoice, p.ClosesAt, IsClosed(p), p.CreatedByUserId,
                names.GetValueOrDefault(p.CreatedByUserId, ""), p.CreatedAt, voters, mine.Count > 0,
                viewerIsMod || p.CreatedByUserId == uid, opts);
        }).ToList();
    }

    // ---------------------------------------------------------------- reputation

    public async Task<IReadOnlyList<ReputationDto>> LeaderboardAsync(Guid communityId, int take, CancellationToken ct = default)
    {
        await RequireReadAsync(communityId, ct);
        take = Math.Clamp(take, 1, 50);
        var rows = await (from r in _db.Set<UserReputation>().AsNoTracking()
                          join u in _db.Set<User>().AsNoTracking() on r.UserId equals u.Id
                          where r.CommunityId == communityId && r.Total > 0
                          orderby r.Total descending, u.DisplayName
                          select new { r.UserId, u.DisplayName, u.AvatarUrl, r.Total, r.Level })
            .Take(take).ToListAsync(ct);
        return rows.Select((r, i) => new ReputationDto(r.UserId, r.DisplayName, r.AvatarUrl, r.Total, r.Level,
            ReputationRules.LevelName(r.Level), i + 1)).ToList();
    }

    public async Task<MyReputationDto> MyReputationAsync(Guid? communityId, CancellationToken ct = default)
    {
        var uid = Uid();
        var total = await _db.Set<UserReputation>().AsNoTracking()
            .Where(r => r.UserId == uid && r.CommunityId == communityId).Select(r => r.Total).FirstOrDefaultAsync(ct);
        var level = ReputationRules.LevelFor(total);
        return new MyReputationDto(total, level, ReputationRules.LevelName(level), ReputationRules.NextLevelAt(total));
    }

    public async Task<IReadOnlyDictionary<Guid, ReputationBadgeDto>> BadgesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().Take(100).ToList();
        var rows = await _db.Set<UserReputation>().AsNoTracking()
            .Where(r => r.CommunityId == null && ids.Contains(r.UserId)).ToListAsync(ct);
        return rows.ToDictionary(r => r.UserId, r => new ReputationBadgeDto(r.Total, r.Level, ReputationRules.LevelName(r.Level)));
    }
}
