using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class PasskeyCredentialConfiguration : IEntityTypeConfiguration<PasskeyCredential>
{
    public void Configure(EntityTypeBuilder<PasskeyCredential> e)
    {
        e.ToTable("passkey_credentials");
        e.Property(x => x.CredentialId).HasMaxLength(1400).IsRequired();
        e.Property(x => x.PublicKey).IsRequired();
        e.Property(x => x.Label).HasMaxLength(100).IsRequired();
        e.HasIndex(x => x.CredentialId).IsUnique();
        e.HasIndex(x => x.UserId);
    }
}
