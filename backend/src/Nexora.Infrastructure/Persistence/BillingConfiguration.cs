using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;

namespace Nexora.Infrastructure.Persistence;

public sealed class SubscriptionConfiguration:IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription>b)
    {
        b.ToTable("subscriptions");b.HasKey(x=>x.Id);b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(30);b.Property(x=>x.BillingInterval).HasConversion<string>().HasMaxLength(20);
        b.Property(x=>x.TrialStartAt).HasColumnType("timestamp with time zone");b.Property(x=>x.TrialEndAt).HasColumnType("timestamp with time zone");b.Property(x=>x.CurrentPeriodStart).HasColumnType("timestamp with time zone");b.Property(x=>x.CurrentPeriodEnd).HasColumnType("timestamp with time zone");b.Property(x=>x.CanceledAt).HasColumnType("timestamp with time zone");b.Property(x=>x.PastDueSince).HasColumnType("timestamp with time zone");b.Property(x=>x.CreatedAt).HasColumnType("timestamp with time zone");b.Property(x=>x.UpdatedAt).HasColumnType("timestamp with time zone");
        b.HasIndex(x=>x.TenantId).IsUnique().HasFilter("\"Status\" IN ('Trialing', 'Active', 'PastDue')");b.HasIndex(x=>new{x.Status,x.TrialEndAt});b.HasIndex(x=>new{x.Status,x.CurrentPeriodEnd});
        b.HasOne<Nexora.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Restrict);b.HasOne<Nexora.Domain.Plans.Plan>().WithMany().HasForeignKey(x=>x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SubscriptionEventConfiguration:IEntityTypeConfiguration<SubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<SubscriptionEvent>b){b.ToTable("subscription_events");b.HasKey(x=>x.Id);b.Property(x=>x.EventType).HasMaxLength(80);b.Property(x=>x.Details).HasMaxLength(1000);b.Property(x=>x.OccurredAt).HasColumnType("timestamp with time zone");b.HasIndex(x=>new{x.SubscriptionId,x.OccurredAt});b.HasOne(x=>x.Subscription).WithMany(x=>x.Events).HasForeignKey(x=>x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);b.HasOne<Nexora.Domain.Identity.User>().WithMany().HasForeignKey(x=>x.ActorUserId).OnDelete(DeleteBehavior.Restrict);}
}

public sealed class PlanPriceConfiguration:IEntityTypeConfiguration<PlanPrice>{public void Configure(EntityTypeBuilder<PlanPrice>b){b.ToTable("plan_prices");b.HasKey(x=>x.Id);b.Property(x=>x.BillingInterval).HasConversion<string>().HasMaxLength(20);b.Property(x=>x.Currency).HasMaxLength(3);b.Property(x=>x.Amount).HasPrecision(18,2);b.HasIndex(x=>new{x.PlanId,x.BillingInterval,x.Currency}).IsUnique().HasFilter("\"IsActive\" = TRUE");b.HasOne<Plan>().WithMany().HasForeignKey(x=>x.PlanId).OnDelete(DeleteBehavior.Restrict);}}
public sealed class BillingInvoiceConfiguration:IEntityTypeConfiguration<BillingInvoice>{public void Configure(EntityTypeBuilder<BillingInvoice>b){b.ToTable("billing_invoices");b.HasKey(x=>x.Id);b.Property(x=>x.BillingInterval).HasConversion<string>().HasMaxLength(20);b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20);b.Property(x=>x.Currency).HasMaxLength(3);b.Property(x=>x.Amount).HasPrecision(18,2);b.Property(x=>x.CoverageStart).HasColumnType("timestamp with time zone");b.Property(x=>x.CoverageEnd).HasColumnType("timestamp with time zone");b.HasIndex(x=>new{x.TenantId,x.CreatedAt});b.HasIndex(x=>new{x.SubscriptionId,x.CoverageStart,x.CoverageEnd}).IsUnique();b.HasOne<Subscription>().WithMany().HasForeignKey(x=>x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);b.HasOne<Plan>().WithMany().HasForeignKey(x=>x.PlanId).OnDelete(DeleteBehavior.Restrict);}}
public sealed class BillingPaymentConfiguration:IEntityTypeConfiguration<BillingPayment>{public void Configure(EntityTypeBuilder<BillingPayment>b){b.ToTable("billing_payments");b.HasKey(x=>x.Id);b.Property(x=>x.Gateway).HasConversion<string>().HasMaxLength(30);b.Property(x=>x.PaymentMethod).HasConversion<string>().HasMaxLength(30);b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(20);b.Property(x=>x.ExternalPaymentId).HasMaxLength(120);b.Property(x=>x.Currency).HasMaxLength(3);b.Property(x=>x.Amount).HasPrecision(18,2);b.HasIndex(x=>new{x.Gateway,x.ExternalPaymentId}).IsUnique();b.HasOne<BillingInvoice>().WithMany().HasForeignKey(x=>x.BillingInvoiceId).OnDelete(DeleteBehavior.Restrict);}}
public sealed class ProcessedWebhookEventConfiguration:IEntityTypeConfiguration<ProcessedWebhookEvent>{public void Configure(EntityTypeBuilder<ProcessedWebhookEvent>b){b.ToTable("processed_webhook_events");b.HasKey(x=>x.Id);b.Property(x=>x.Provider).HasConversion<string>().HasMaxLength(30);b.Property(x=>x.ExternalEventId).HasMaxLength(150);b.Property(x=>x.EventType).HasMaxLength(100);b.HasIndex(x=>new{x.Provider,x.ExternalEventId}).IsUnique();}}
