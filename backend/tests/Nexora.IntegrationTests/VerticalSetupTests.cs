using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Tenancy;
using Nexora.Domain.Administration;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class VerticalSetupTests
{
    [Fact]
    public async Task TemplateIsDataDrivenOptionalIdempotentAndTenantScoped()
    {
        await using var factory = new ApiFactory();
        await TestFeatureCatalog.GrantAllModulesAsync(factory);
        var client = factory.CreateClient();
        var token = await Register(client, "vertical@nexora.test");
        var tenantId = await CreateTenant(client, token, "vertical-shop");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var segment = new BusinessSegment("BARBERSHOP_SALON", "Barbearia / Salão", DateTimeOffset.UtcNow);
            db.BusinessSegments.Add(segment);
            var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
            db.Entry(tenant).Property(x => x.SegmentId).CurrentValue = segment.Id;
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", await Select(client, token, "vertical-shop"));
        var setup = await client.GetFromJsonAsync<Setup>("/api/v1/vertical-setup");
        Assert.Contains(setup!.ServicePresets, x => x.Code == "HAIRCUT" && x.Category == "Cabelo");
        var apply = new { services = new[] { new { presetCode = "HAIRCUT", durationMinutes = 30, price = 45m } } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/vertical-setup/apply", apply)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/vertical-setup/apply", apply)).StatusCode);
        var services = await client.GetFromJsonAsync<ServiceDto[]>("/api/v1/services");
        Assert.Single(services!); Assert.Equal(45m, services![0].Price);
    }

    private static async Task<string> Register(HttpClient client, string email)
    { var response=await client.PostAsJsonAsync("/api/v1/identity/register",new{email,password="Correct-Horse-42"});response.EnsureSuccessStatusCode();Cookie(client,response);return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken; }
    private static async Task<Guid> CreateTenant(HttpClient client,string token,string slug)
    { client.DefaultRequestHeaders.Authorization=new("Bearer",token);var response=await client.PostAsJsonAsync("/api/v1/tenants",new{name=slug,slug,timeZoneId="UTC"});response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<TenantDto>())!.Id; }
    private static async Task<string> Select(HttpClient client,string token,string slug)
    { client.DefaultRequestHeaders.Authorization=new("Bearer",token);var response=await client.PostAsJsonAsync($"/api/v1/t/{slug}/session",new{});response.EnsureSuccessStatusCode();client.DefaultRequestHeaders.Remove("Cookie");Cookie(client,response);return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken; }
    private static void Cookie(HttpClient client,HttpResponseMessage response){client.DefaultRequestHeaders.Remove("Cookie");client.DefaultRequestHeaders.Add("Cookie",response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("nexora_refresh=",StringComparison.Ordinal)).Split(';',2)[0]);}
    private sealed record Token(string AccessToken); private sealed record TenantDto(Guid Id); private sealed record Setup(Preset[] ServicePresets); private sealed record Preset(string Code,string Category); private sealed record ServiceDto(decimal Price);
}
