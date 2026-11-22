using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Pets;

namespace Pawzaroo.Infrastructure.Modules.Pets;

public class LostFoundDetailExtraConfiguration : IEntityTypeConfiguration<LostFoundDetailExtra>
{
    public void Configure(EntityTypeBuilder<LostFoundDetailExtra> e)
    {
        e.ToTable("lost_found_detail_extras");
        e.HasIndex(x => x.ReportId).IsUnique();
        e.HasIndex(x => x.MicrochipNumber);
        e.Property(x => x.Markings).HasMaxLength(500);
        e.Property(x => x.CollarDescription).HasMaxLength(300);
        e.Property(x => x.Sex).HasMaxLength(16);
        e.Property(x => x.AgeEstimate).HasMaxLength(64);
        e.Property(x => x.MicrochipNumber).HasMaxLength(32);
    }
}

public class LostFoundSightingConfiguration : IEntityTypeConfiguration<LostFoundSighting>
{
    public void Configure(EntityTypeBuilder<LostFoundSighting> e)
    {
        e.ToTable("lost_found_sightings");
        e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        e.Property(x => x.PhotoUrl).HasMaxLength(1000);
        e.HasIndex(x => new { x.ReportId, x.CreatedAt });
    }
}

public class LostFoundEvidenceConfiguration : IEntityTypeConfiguration<LostFoundEvidence>
{
    public void Configure(EntityTypeBuilder<LostFoundEvidence> e)
    {
        e.ToTable("lost_found_evidence");
        e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        e.HasIndex(x => x.ReportId);
    }
}

public class LostFoundAlertSubscriptionConfiguration : IEntityTypeConfiguration<LostFoundAlertSubscription>
{
    public void Configure(EntityTypeBuilder<LostFoundAlertSubscription> e)
    {
        e.ToTable("lost_found_alert_subscriptions");
        e.HasIndex(x => new { x.Active, x.Latitude, x.Longitude });
        e.HasIndex(x => x.UserId);
    }
}
