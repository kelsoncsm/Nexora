using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Identity;

namespace Nexora.Infrastructure.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", DatabaseSchemas.Identity); b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    { b.ToTable("roles", DatabaseSchemas.Identity); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100).IsRequired(); b.HasIndex(x => x.Name).IsUnique(); }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    { b.ToTable("permissions", DatabaseSchemas.Identity); b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(150).IsRequired(); b.HasIndex(x => x.Name).IsUnique(); }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions", DatabaseSchemas.Identity); b.HasKey(x => new { x.RoleId, x.PermissionId });
        b.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleId);
        b.HasOne(x => x.Permission).WithMany(x => x.Roles).HasForeignKey(x => x.PermissionId);
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles", DatabaseSchemas.Identity); b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId);
        b.HasOne(x => x.Role).WithMany(x => x.Users).HasForeignKey(x => x.RoleId);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens", DatabaseSchemas.Identity); b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.FamilyId });
        b.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.IsRevoked);
    }
}
