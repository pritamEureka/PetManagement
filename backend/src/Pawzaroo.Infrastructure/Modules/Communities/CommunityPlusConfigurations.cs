using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Communities;

namespace Pawzaroo.Infrastructure.Modules.Communities;

public class CommunityPollConfiguration : IEntityTypeConfiguration<CommunityPoll>
{
    public void Configure(EntityTypeBuilder<CommunityPoll> e)
    {
        e.ToTable("community_polls");
        e.Property(x => x.Question).HasMaxLength(300).IsRequired();
        e.HasIndex(x => new { x.CommunityId, x.CreatedAt });
    }
}

public class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> e)
    {
        e.ToTable("community_poll_options");
        e.Property(x => x.Text).HasMaxLength(160).IsRequired();
        e.HasOne(x => x.Poll).WithMany(p => p.Options).HasForeignKey(x => x.PollId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => x.PollId);
    }
}

public class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> e)
    {
        e.ToTable("community_poll_votes");
        e.HasIndex(x => new { x.UserId, x.OptionId }).IsUnique();
        e.HasIndex(x => new { x.PollId, x.UserId });
        e.HasIndex(x => x.OptionId);
    }
}

public class ReputationEventConfiguration : IEntityTypeConfiguration<ReputationEvent>
{
    public void Configure(EntityTypeBuilder<ReputationEvent> e)
    {
        e.ToTable("reputation_events");
        e.HasIndex(x => new { x.UserId, x.Kind, x.SourceId }).IsUnique();
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasIndex(x => x.CommunityId);
    }
}

public class UserReputationConfiguration : IEntityTypeConfiguration<UserReputation>
{
    public void Configure(EntityTypeBuilder<UserReputation> e)
    {
        e.ToTable("user_reputations");
        // Postgres treats NULLs as distinct, so the global (null community) row is guarded in the service.
        e.HasIndex(x => new { x.UserId, x.CommunityId }).IsUnique();
        e.HasIndex(x => new { x.CommunityId, x.Total });
    }
}
