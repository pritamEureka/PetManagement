using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pawzaroo.Domain.Identity;

namespace Pawzaroo.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.Email).HasMaxLength(256).IsRequired();
        e.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        e.Property(x => x.UserName).HasMaxLength(64);
        e.Property(x => x.AvatarUrl).HasMaxLength(512);
        e.Property(x => x.PhoneNumber).HasMaxLength(32);
        e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> e)
    {
        e.ToTable("roles");
        e.HasIndex(x => x.Name).IsUnique();
        e.Property(x => x.Name).HasMaxLength(64).IsRequired();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> e)
    {
        e.ToTable("permissions");
        e.HasIndex(x => new { x.Module, x.Action }).IsUnique();
        e.Property(x => x.Module).HasMaxLength(64).IsRequired();
        e.Property(x => x.Action).HasMaxLength(64).IsRequired();
        e.Ignore(x => x.Code);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> e)
    {
        e.ToTable("user_roles");
        e.HasKey(x => new { x.UserId, x.RoleId });
        e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> e)
    {
        e.ToTable("role_permissions");
        e.HasKey(x => new { x.RoleId, x.PermissionId });
        e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("refresh_tokens");
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.Ignore(x => x.IsActive);
    }
}
