using System.Net;

namespace Nexora.IntegrationTests;

public sealed class HealthCheckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task LiveHealthCheckReturnsOk()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
