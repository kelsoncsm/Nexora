using System.Net;
using System.Net.Http.Json;

namespace Nexora.IntegrationTests;

public sealed class CatalogIsolationTests
{
    [Fact]
    public async Task CrudLinkValidationAndTenantIsolation()
    {
        await using var factory = new ApiFactory(); var a = factory.CreateClient(); var b = factory.CreateClient();
        await TestFeatureCatalog.GrantAllModulesAsync(factory);
        var globalA = await Register(a, "catalog-a@nexora.test"); var globalB = await Register(b, "catalog-b@nexora.test");
        await CreateTenant(a, globalA, "catalog-a"); await CreateTenant(b, globalB, "catalog-b");
        a.DefaultRequestHeaders.Authorization = new("Bearer", await Select(a, globalA, "catalog-a"));
        b.DefaultRequestHeaders.Authorization = new("Bearer", await Select(b, globalB, "catalog-b"));
        var professional = await Post<ProfessionalDto>(a, "/api/v1/professionals", new { name="Ana",email="ana@test.local",phone="1",isActive=true });
        var service = await Post<ServiceDto>(a, "/api/v1/services", new { name="Consulta",description="Desc",durationMinutes=30,price=99.90m,isActive=true });
        Assert.Equal(HttpStatusCode.NoContent,(await a.PutAsync($"/api/v1/professionals/{professional.Id}/services/{service.Id}",null)).StatusCode);
        Assert.Contains(service.Id,(await a.GetFromJsonAsync<ProfessionalDto[]>("/api/v1/professionals"))!.Single().ServiceIds);
        Assert.Equal(HttpStatusCode.NotFound,(await b.PutAsync($"/api/v1/professionals/{professional.Id}/services/{service.Id}",null)).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<ProfessionalDto[]>("/api/v1/professionals"))!);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync("/api/v1/services",new{name="Invalid",description="",durationMinutes=0,price=-1,isActive=true})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await a.DeleteAsync($"/api/v1/professionals/{professional.Id}/services/{service.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await a.PutAsJsonAsync($"/api/v1/services/{service.Id}",new{name="Consulta 2",description="Updated",durationMinutes=45,price=120m,isActive=true})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await a.DeleteAsync($"/api/v1/services/{service.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await a.DeleteAsync($"/api/v1/professionals/{professional.Id}")).StatusCode);
    }
    private static async Task<T> Post<T>(HttpClient client,string uri,object body){var response=await client.PostAsJsonAsync(uri,body);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<string> Register(HttpClient client,string email){var response=await client.PostAsJsonAsync("/api/v1/identity/register",new{email,password="Correct-Horse-42"});response.EnsureSuccessStatusCode();SetCookie(client,response);return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;}
    private static async Task CreateTenant(HttpClient client,string token,string slug){client.DefaultRequestHeaders.Authorization=new("Bearer",token);(await client.PostAsJsonAsync("/api/v1/tenants",new{name=slug,slug,timeZoneId="UTC"})).EnsureSuccessStatusCode();}
    private static async Task<string> Select(HttpClient client,string token,string slug){client.DefaultRequestHeaders.Authorization=new("Bearer",token);var response=await client.PostAsJsonAsync($"/api/v1/t/{slug}/session",new{});response.EnsureSuccessStatusCode();client.DefaultRequestHeaders.Remove("Cookie");SetCookie(client,response);return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;}
    private static void SetCookie(HttpClient client,HttpResponseMessage response)=>client.DefaultRequestHeaders.Add("Cookie",response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("nexora_refresh=",StringComparison.Ordinal)).Split(';',2)[0]);
    private sealed record Token(string AccessToken); private sealed record ProfessionalDto(Guid Id,Guid[] ServiceIds); private sealed record ServiceDto(Guid Id);
}
