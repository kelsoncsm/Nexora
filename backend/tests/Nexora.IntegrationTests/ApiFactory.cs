using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexora.Infrastructure.Persistence;
using Nexora.Application.Plans;
using Nexora.Application.Billing;
using Nexora.Domain.Billing;

namespace Nexora.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly bool useStubPlanProvider;
    public ApiFactory():this(true){}
    private ApiFactory(bool useStubPlanProvider)=>this.useStubPlanProvider=useStubPlanProvider;
    public static ApiFactory WithPersistentPlanProvider()=>new(false);
    public StubTenantPlanProvider PlanProvider { get; } = new();
    public FakePaymentGateway PaymentGateway { get; } = new();
    private readonly string databaseName = $"nexora-{Guid.NewGuid()}";
    private static readonly ServiceProvider InMemoryProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:NexoraDatabase",
            "Host=127.0.0.1;Port=1;Database=nexora_tests;Username=test;Password=test");
        builder.UseSetting("Identity:Issuer", "Nexora.Tests");
        builder.UseSetting("Identity:Audience", "Nexora.Tests.Client");
        builder.UseSetting("Identity:SigningKey", "integration-test-signing-key-with-more-than-32-characters");
        builder.UseSetting("Payments:MercadoPago:WebhookSecret", "test-webhook-secret");
        builder.UseSetting("Email:Provider", "Fake");
        builder.UseSetting("Email:WorkerEnabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NexoraDbContext>();
            services.RemoveAll<DbContextOptions<NexoraDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<NexoraDbContext>(options =>
                options.UseInMemoryDatabase(databaseName)
                    .UseInternalServiceProvider(InMemoryProvider));
            if(useStubPlanProvider){services.RemoveAll<ITenantPlanProvider>();services.AddSingleton<ITenantPlanProvider>(PlanProvider);}
            services.RemoveAll<IPaymentGateway>();services.AddSingleton<IPaymentGateway>(PaymentGateway);
        });
    }
}

public sealed class FakePaymentGateway:IPaymentGateway
{
    private int createCalls;public System.Collections.Concurrent.ConcurrentQueue<GatewayCheckoutRequest> Requests{get;}=new();
    public GatewayProvider Provider=>GatewayProvider.MercadoPago;public int CreateCalls=>Volatile.Read(ref createCalls);public GatewayCheckoutResult CheckoutResult{get;set;}=new("fake-payment",BillingPaymentStatus.Pending,"https://checkout.test",null,DateTimeOffset.UtcNow);public GatewayPaymentResult PaymentResult{get;set;}=new("fake-payment",BillingPaymentStatus.Pending,99.90m,"BRL",PaymentMethod.Pix,DateTimeOffset.UtcNow);public Exception? CreateException{get;set;}
    public Task<GatewayCheckoutResult>CreateCheckoutAsync(GatewayCheckoutRequest request,CancellationToken ct){Interlocked.Increment(ref createCalls);Requests.Enqueue(request);if(CreateException is not null)throw CreateException;return Task.FromResult(CheckoutResult);}
    public Task<GatewayPaymentResult>GetPaymentAsync(string externalPaymentId,CancellationToken ct)=>Task.FromResult(PaymentResult);
}

public sealed class StubTenantPlanProvider : ITenantPlanProvider
{
    public Guid? PlanId { get; set; }
    public Task<Guid?> GetCurrentPlanIdAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(PlanId);
}
