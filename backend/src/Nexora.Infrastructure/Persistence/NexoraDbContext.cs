using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Identity;
using Nexora.Domain.Tenancy;
using Nexora.Domain.Administration;
using Nexora.Domain.Plans;
using Nexora.Domain.Customers;
using Nexora.Domain.Catalog;
using Nexora.Domain.Scheduling;
using Nexora.Domain.Billing;
using Nexora.Domain.Notifications;
using Nexora.Domain.Onboarding;

namespace Nexora.Infrastructure.Persistence;

public sealed class NexoraDbContext(DbContextOptions<NexoraDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();
    public DbSet<BusinessSegment> BusinessSegments => Set<BusinessSegment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Professional> Professionals=>Set<Professional>();public DbSet<Service> Services=>Set<Service>();public DbSet<ProfessionalService> ProfessionalServices=>Set<ProfessionalService>();
    public DbSet<WorkingHours> WorkingHours=>Set<WorkingHours>();public DbSet<BlockedPeriod> BlockedPeriods=>Set<BlockedPeriod>();public DbSet<Appointment> Appointments=>Set<Appointment>();
    public DbSet<Subscription> Subscriptions=>Set<Subscription>();public DbSet<SubscriptionEvent> SubscriptionEvents=>Set<SubscriptionEvent>();
    public DbSet<PlanPrice> PlanPrices=>Set<PlanPrice>();public DbSet<BillingInvoice> BillingInvoices=>Set<BillingInvoice>();public DbSet<BillingPayment> BillingPayments=>Set<BillingPayment>();public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents=>Set<ProcessedWebhookEvent>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<OnboardingDraft> OnboardingDrafts => Set<OnboardingDraft>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NexoraDbContext).Assembly);
    }
}
