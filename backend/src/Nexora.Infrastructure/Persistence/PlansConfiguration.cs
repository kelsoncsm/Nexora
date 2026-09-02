using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Plans;

namespace Nexora.Infrastructure.Persistence;

public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature> { public void Configure(EntityTypeBuilder<Feature> b) { b.ToTable("features"); b.HasKey(x=>x.Id); b.Property(x=>x.Code).HasMaxLength(80).IsRequired(); b.HasIndex(x=>x.Code).IsUnique(); b.Property(x=>x.Name).HasMaxLength(120).IsRequired(); } }
public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan> { public void Configure(EntityTypeBuilder<Plan> b) { b.ToTable("plans"); b.HasKey(x=>x.Id); b.Property(x=>x.Code).HasMaxLength(80).IsRequired(); b.HasIndex(x=>x.Code).IsUnique(); b.Property(x=>x.Name).HasMaxLength(120).IsRequired(); } }
public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature> { public void Configure(EntityTypeBuilder<PlanFeature> b) { b.ToTable("plan_features"); b.HasKey(x=>new{x.PlanId,x.FeatureId}); b.HasOne(x=>x.Plan).WithMany(x=>x.Features).HasForeignKey(x=>x.PlanId); b.HasOne(x=>x.Feature).WithMany().HasForeignKey(x=>x.FeatureId).OnDelete(DeleteBehavior.Restrict); } }
public sealed class TenantFeatureOverrideConfiguration : IEntityTypeConfiguration<TenantFeatureOverride> { public void Configure(EntityTypeBuilder<TenantFeatureOverride> b) { b.ToTable("tenant_feature_overrides"); b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.TenantId,x.FeatureId}).IsUnique(); b.HasOne<Nexora.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Cascade); b.HasOne(x=>x.Feature).WithMany().HasForeignKey(x=>x.FeatureId).OnDelete(DeleteBehavior.Restrict); } }
