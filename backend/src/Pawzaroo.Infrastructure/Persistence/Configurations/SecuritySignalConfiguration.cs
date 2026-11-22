using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class SecuritySignalConfiguration : IEntityTypeConfiguration<SecuritySignal>
{
    public void Configure(EntityTypeBuilder<SecuritySignal> e)
    {
        e.ToTable("security_signals");
        e.Property(x => x.Type).HasMaxLength(64).IsRequired();
        e.Property(x => x.Severity).HasMaxLength(16).IsRequired();
        e.Property(x => x.Ip).HasMaxLength(64);
        e.HasIndex(x => x.CreatedAt);
        e.HasIndex(x => new { x.Type, x.CreatedAt });
        e.HasIndex(x => x.UserId);
    }
}
