using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexora.Application.Billing;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class PostgresApiFactory:WebApplicationFactory<Program>
{
    private readonly string connectionString=Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES")??throw new InvalidOperationException("NEXORA_HARDENING_POSTGRES is required.");
    public FakePaymentGateway PaymentGateway{get;}=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");builder.UseSetting("ConnectionStrings:NexoraDatabase",connectionString);builder.UseSetting("Identity:Issuer","Nexora.Postgres.Tests");builder.UseSetting("Identity:Audience","Nexora.Postgres.Tests.Client");builder.UseSetting("Identity:SigningKey","postgres-integration-test-key-with-more-than-32-characters");builder.UseSetting("Payments:MercadoPago:WebhookSecret","test-webhook-secret");builder.UseSetting("Email:Provider","Fake");builder.UseSetting("Email:WorkerEnabled","false");
        builder.ConfigureServices(services=>{services.RemoveAll<NexoraDbContext>();services.RemoveAll<DbContextOptions<NexoraDbContext>>();services.RemoveAll<IDatabaseProvider>();services.AddDbContext<NexoraDbContext>(options=>options.UseNpgsql(connectionString));services.RemoveAll<IPaymentGateway>();services.AddSingleton<IPaymentGateway>(PaymentGateway);});
    }
    public async Task InitializeAsync(){await using var scope=Services.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().Database.MigrateAsync();}
}
