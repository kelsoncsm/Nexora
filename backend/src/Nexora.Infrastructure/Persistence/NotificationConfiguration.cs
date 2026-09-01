using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Notifications;

namespace Nexora.Infrastructure.Persistence;

public sealed class EmailOutboxMessageConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox_messages"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired(); builder.Property(x => x.Recipient).HasMaxLength(320).IsRequired();
        builder.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired(); builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired(); builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.Property(x => x.ExternalMessageId).HasMaxLength(150); builder.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique(); builder.HasIndex(x => new { x.Status, x.NextAttemptAt }); builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasOne<Nexora.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
