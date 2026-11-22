using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class BackupRecoveryCodeConfiguration : IEntityTypeConfiguration<BackupRecoveryCode>
{
    public void Configure(EntityTypeBuilder<BackupRecoveryCode> e)
    {
        e.ToTable("backup_recovery_codes");
        e.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
        e.HasIndex(x => new { x.UserId, x.CodeHash });
    }
}

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> e)
    {
        e.ToTable("consent_records");
        e.Property(x => x.ConsentType).HasMaxLength(64).IsRequired();
        e.Property(x => x.Version).HasMaxLength(32);
        e.Property(x => x.IpAddress).HasMaxLength(64);
        e.Property(x => x.UserAgent).HasMaxLength(512);
        e.HasIndex(x => new { x.UserId, x.RecordedAt });
    }
}

public class StepUpVerificationConfiguration : IEntityTypeConfiguration<StepUpVerification>
{
    public void Configure(EntityTypeBuilder<StepUpVerification> e)
    {
        e.ToTable("step_up_verifications");
        e.Property(x => x.Method).HasMaxLength(16).IsRequired();
        e.HasIndex(x => new { x.UserId, x.VerifiedAt });
    }
}
