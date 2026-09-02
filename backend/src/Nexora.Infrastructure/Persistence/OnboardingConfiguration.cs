using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Onboarding;

namespace Nexora.Infrastructure.Persistence;

public sealed class OnboardingDraftConfiguration : IEntityTypeConfiguration<OnboardingDraft>
{
    public void Configure(EntityTypeBuilder<OnboardingDraft> builder)
    {
        builder.ToTable("onboarding_drafts", DatabaseSchemas.Onboarding); builder.HasKey(x => x.Id);
        builder.Property(x => x.CompanyName).HasMaxLength(200); builder.Property(x => x.CompanySlug).HasMaxLength(100);
        builder.Property(x => x.TimeZoneId).HasMaxLength(100); builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BillingInterval).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.UserId, x.Status }).IsUnique().HasFilter("\"Status\" = 'InProgress'");
        builder.HasIndex(x => x.CompletedTenantId).IsUnique().HasFilter("\"CompletedTenantId\" IS NOT NULL");
        builder.HasOne<Nexora.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Nexora.Domain.Administration.BusinessSegment>().WithMany().HasForeignKey(x => x.SegmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Nexora.Domain.Plans.Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Nexora.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.CompletedTenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
