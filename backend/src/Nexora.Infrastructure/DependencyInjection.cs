using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Nexora.Application.Identity;
using Nexora.Domain.Identity;
using Nexora.Infrastructure.Identity;
using NexoraIdentityOptions = Nexora.Infrastructure.Identity.IdentityOptions;
using Nexora.Application.Tenancy;
using Nexora.Infrastructure.Tenancy;
using Nexora.Application.Administration;
using Nexora.Infrastructure.Administration;
using Nexora.Application.Plans;
using Nexora.Infrastructure.Plans;
using Nexora.Application.Customers;using Nexora.Infrastructure.Customers;
using Nexora.Application.Catalog;using Nexora.Infrastructure.Catalog;
using Nexora.Application.Scheduling;using Nexora.Infrastructure.Scheduling;
using Nexora.Application.Billing;using Nexora.Infrastructure.Billing;
using Nexora.Application.Notifications;
using Nexora.Infrastructure.Notifications;
using Nexora.Application.Reports;
using Nexora.Infrastructure.Reports;
using Nexora.Application.Onboarding;
using Nexora.Infrastructure.Onboarding;
using Nexora.Application.Verticals;
using Nexora.Infrastructure.Verticals;

namespace Nexora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NexoraDatabase")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:NexoraDatabase must be configured.");

        services.AddDbContext<NexoraDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddOptions<NexoraIdentityOptions>()
            .Bind(configuration.GetSection(NexoraIdentityOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Identity:Issuer is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Identity:Audience is required.")
            .Validate(x => x.SigningKey.Length >= 32, "Identity:SigningKey must contain at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAccessTokenGenerator, AccessTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(x => x.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextInitializer>(x => x.GetRequiredService<TenantContext>());
        services.AddScoped<ITenancyService, TenancyService>();
        services.AddScoped<IAdministrationService, AdministrationService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IPlatformAdminBootstrapper, PlatformAdminBootstrapper>();
        services.AddScoped<PersistentTenantPlanProvider>();
        services.AddScoped<ITenantPlanProvider>(x=>x.GetRequiredService<PersistentTenantPlanProvider>());
        services.AddScoped<IFeatureAccessService, FeatureAccessService>();
        services.AddScoped<IPlanCatalogService, PlanCatalogService>();
        services.AddScoped<ICustomerService,CustomerService>();
        services.AddScoped<ICatalogService,CatalogService>();
        services.AddScoped<ITenantTimeZoneProvider,TenantTimeZoneProvider>();
        services.AddSingleton<ITimeZoneService,TimeZoneService>();
        services.AddScoped<ISchedulingService,SchedulingService>();
        services.AddScoped<ISubscriptionService,SubscriptionService>();
        services.AddOptions<MercadoPagoOptions>().Bind(configuration.GetSection(MercadoPagoOptions.SectionName)).Validate(x=>x.WebhookTimestampToleranceSeconds>0,"Payments:MercadoPago:WebhookTimestampToleranceSeconds must be positive.").ValidateOnStart();
        services.AddHttpClient<IPaymentGateway,MercadoPagoPaymentGateway>((sp,http)=>http.BaseAddress=new Uri(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MercadoPagoOptions>>().Value.BaseUrl));
        services.AddScoped<IWebhookSignatureValidator,MercadoPagoWebhookSignatureValidator>();
        services.AddScoped<IBillingPaymentService,BillingPaymentService>();
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(x => x.PollIntervalSeconds > 0, "Email:PollIntervalSeconds must be positive.")
            .Validate(x => x.MaxAttempts > 0, "Email:MaxAttempts must be positive.")
            .Validate(x => x.RetryDelaysSeconds.Length > 0 && x.RetryDelaysSeconds.All(delay => delay > 0), "Email retry delays must be positive.")
            .Validate(x => !string.Equals(x.Provider, "Resend", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(x.FromAddress) && !string.IsNullOrWhiteSpace(x.ResendApiKey)),
                "Email:FromAddress and Email:ResendApiKey are required for Resend.")
            .ValidateOnStart();
        services.AddSingleton<FakeEmailSender>();
        services.AddHttpClient<ResendEmailSender>(client => client.BaseAddress = new Uri("https://api.resend.com/"));
        services.AddScoped<IEmailSender>(provider =>
            string.Equals(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value.Provider, "Resend", StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<ResendEmailSender>()
                : provider.GetRequiredService<FakeEmailSender>());
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<EmailOutboxProcessor>();
        services.AddHostedService<EmailOutboxWorker>();
        services.AddScoped<EmailOutboxHealthCheck>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddOptions<VerticalSetupOptions>().Bind(configuration.GetSection(VerticalSetupOptions.SectionName));
        services.AddScoped<IVerticalSetupService, VerticalSetupService>();

        services
            .AddHealthChecks()
            .AddDbContextCheck<NexoraDbContext>(
                name: "postgresql",
                tags: ["ready"])
            .AddCheck<EmailOutboxHealthCheck>("email-outbox", tags: ["observability"]);

        return services;
    }
}
