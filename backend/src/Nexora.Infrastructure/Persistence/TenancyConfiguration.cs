using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Tenancy;

namespace Nexora.Infrastructure.Persistence;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        b.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasOne<Nexora.Domain.Administration.BusinessSegment>().WithMany().HasForeignKey(x => x.SegmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> b)
    {
        b.ToTable("tenant_users"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.IsActive });
        b.HasOne(x => x.Tenant).WithMany(x => x.Users).HasForeignKey(x => x.TenantId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.HasOne(x => x.TenantRole).WithMany(x=>x.Users).HasForeignKey(x => x.TenantRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenantRoleConfiguration:IEntityTypeConfiguration<TenantRole>{public void Configure(EntityTypeBuilder<TenantRole>b){b.ToTable("tenant_roles");b.HasKey(x=>x.Id);b.Property(x=>x.Name).HasMaxLength(100).IsRequired();b.Property(x=>x.Description).HasMaxLength(300);b.HasIndex(x=>new{x.TenantId,x.Name}).IsUnique();b.HasOne(x=>x.Tenant).WithMany(x=>x.Roles).HasForeignKey(x=>x.TenantId);}}
public sealed class TenantRolePermissionConfiguration:IEntityTypeConfiguration<TenantRolePermission>{public void Configure(EntityTypeBuilder<TenantRolePermission>b){b.ToTable("tenant_role_permissions");b.HasKey(x=>new{x.TenantRoleId,x.PermissionKey});b.Property(x=>x.PermissionKey).HasMaxLength(150);b.HasOne(x=>x.TenantRole).WithMany(x=>x.Permissions).HasForeignKey(x=>x.TenantRoleId);}}

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> b)
    {
        b.ToTable("tenant_invitations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(320).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => x.TokenHash).IsUnique();
        // At most one pending invitation per (tenant, e-mail): enforced in code and by this filtered
        // unique index so a race cannot create two.
        b.HasIndex(x => new { x.TenantId, x.NormalizedEmail })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TenantRole).WithMany().HasForeignKey(x => x.TenantRoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
