using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Api.Configuration;

namespace Nexora.IntegrationTests;

public sealed class ProductionHardeningTests
{
    [Fact]
    public async Task ResponsesContainSecurityAndCorrelationHeaders()
    {
        await using var factory=new ApiFactory();using var client=factory.CreateClient();var response=await client.GetAsync("/health/live");
        response.EnsureSuccessStatusCode();Assert.Equal("nosniff",response.Headers.GetValues("X-Content-Type-Options").Single());Assert.Equal("DENY",response.Headers.GetValues("X-Frame-Options").Single());Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("X-Correlation-ID").Single()));
    }

    [Fact]
    public async Task SensitiveIdentityEndpointsAreRateLimitedPerClientAddress()
    {
        await using var factory=new ApiFactory();using var client=factory.CreateClient();HttpResponseMessage? response=null;
        for(var attempt=0;attempt<11;attempt++)response=await client.PostAsJsonAsync("/api/v1/identity/login",new{email="missing@nexora.test",password="Correct-Horse-42"});
        Assert.Equal(HttpStatusCode.TooManyRequests,response!.StatusCode);Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public void ProductionRejectsFakeProvidersWildcardsAndMissingSecrets()
    {
        var values=new Dictionary<string,string?> { ["ConnectionStrings:NexoraDatabase"]="Host=db",["Identity:SigningKey"]="short",["Email:Provider"]="Fake",["AllowedHosts"]="*",["Cors:AllowedOrigins:0"]="*" };
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(values).Build();var environment=new StubEnvironment("Production");
        var exception=Assert.Throws<InvalidOperationException>(()=>configuration.ValidateProductionConfiguration(environment));
        Assert.Contains("Email:Provider",exception.Message,StringComparison.Ordinal);Assert.Contains("Cors:AllowedOrigins",exception.Message,StringComparison.Ordinal);Assert.DoesNotContain("Host=db",exception.Message,StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadinessDoesNotDependOnEmailOrPaymentProviders()
    {
        await using var factory=new ApiFactory();factory.PaymentGateway.CreateException=new InvalidOperationException("provider unavailable");factory.Services.GetRequiredService<Nexora.Infrastructure.Notifications.FakeEmailSender>().Behavior=Nexora.Infrastructure.Notifications.FakeEmailBehavior.Timeout;
        using var client=factory.CreateClient();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);
    }

    private sealed class StubEnvironment(string name):IHostEnvironment
    { public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="Nexora.Tests";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider(); }
}
