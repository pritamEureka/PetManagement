using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Intelligence;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class ScheduledReportConfiguration : IEntityTypeConfiguration<ScheduledReport>
{
    public void Configure(EntityTypeBuilder<ScheduledReport> e)
    {
        e.ToTable("scheduled_reports");
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Dataset).HasMaxLength(32).IsRequired();
        e.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(16);
        e.HasIndex(x => new { x.IsActive, x.NextRunAt });
        e.HasIndex(x => x.OwnerUserId);
    }
}

public class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> e)
    {
        e.ToTable("report_runs");
        e.Property(x => x.Error).HasMaxLength(1000);
        e.HasOne(x => x.ScheduledReport).WithMany(r => r.Runs).HasForeignKey(x => x.ScheduledReportId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.ScheduledReportId, x.RanAt });
    }
}
