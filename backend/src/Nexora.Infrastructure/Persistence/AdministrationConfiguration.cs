using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Administration;

namespace Nexora.Infrastructure.Persistence;

public sealed class BusinessSegmentConfiguration : IEntityTypeConfiguration<BusinessSegment>
{
    public void Configure(EntityTypeBuilder<BusinessSegment> b)
    {
        b.ToTable("business_segments", DatabaseSchemas.Platform); b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired(); b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs", DatabaseSchemas.Platform); b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(150).IsRequired();
        b.Property(x => x.TargetType).HasMaxLength(100).IsRequired();
        b.Property(x => x.TargetId).HasMaxLength(100).IsRequired();
        b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.OccurredAt); b.HasIndex(x => x.ActorUserId);
        b.HasOne<Nexora.Domain.Identity.User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
