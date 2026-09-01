using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Nexora.Api.Configuration;
using Nexora.Api.Identity;
using Nexora.Api.Tenancy;
using Nexora.Api.Administration;
using Nexora.Application.Administration;
using Nexora.Api.Customers;
using Nexora.Api.Catalog;
using Nexora.Api.Scheduling;
using Nexora.Api.Billing;
using Nexora.Api.Reports;
using Nexora.Api.Onboarding;
using Nexora.Api.Verticals;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateProductionConfiguration(builder.Environment);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

await app.ApplyDevelopmentMigrationsAsync();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<IPlatformAdminBootstrapper>().BootstrapAsync(CancellationToken.None);

app.UseApiPipeline();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapHealthChecks("/health/observability", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("observability")
});
app.MapIdentityEndpoints();
app.MapTenancyEndpoints();
app.MapAdministrationEndpoints();
app.MapCustomerEndpoints();
app.MapCatalogEndpoints();
app.MapVerticalSetupEndpoints();
app.MapSchedulingEndpoints();
app.MapBillingEndpoints();
app.MapReportEndpoints();
app.MapOnboardingEndpoints();

app.Run();

public partial class Program;
