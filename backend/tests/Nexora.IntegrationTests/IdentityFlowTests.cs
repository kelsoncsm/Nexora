using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Identity;

namespace Nexora.IntegrationTests;

public sealed class IdentityFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Email = "identity@nexora.test";
    private const string Password = "Correct-Horse-42";

    [Fact]
    public async Task CompleteIdentityLifecycleIsProtectedAndRotatesRefreshToken()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/identity/me")).StatusCode);

        var invalid = await client.PostAsJsonAsync("/api/v1/identity/register", new { email = Email, password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var registration = await client.PostAsJsonAsync("/api/v1/identity/register", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registeredToken = await ReadTokenAsync(registration);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registeredToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/identity/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var badLogin = await client.PostAsJsonAsync("/api/v1/identity/login", new { email = Email, password = "Wrong-Password-42" });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadTokenAsync(login)));
        client.DefaultRequestHeaders.Add("Cookie", ReadRefreshCookie(login));

        var refresh = await client.PostAsJsonAsync("/api/v1/identity/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadTokenAsync(refresh)));
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", ReadRefreshCookie(refresh));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/identity/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/identity/refresh", new { })).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutPermissionReceivesForbidden()
    {
        using var scope = factory.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var token = generator.Generate(Guid.NewGuid(), "no-permission@nexora.test", [], []).Token;
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/identity/me")).StatusCode);
    }

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return body?.AccessToken ?? string.Empty;
    }
    private static string ReadRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal))
            .Split(';', 2)[0];
    private sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
}
