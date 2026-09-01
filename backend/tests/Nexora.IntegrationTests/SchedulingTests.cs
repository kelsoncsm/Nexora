using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Scheduling;

namespace Nexora.IntegrationTests;

public sealed class SchedulingTests
{
    [Fact]
    public async Task TimeZoneConversionCoversIanaDstAndDifferentTenants()
    {
        await using var factory=new ApiFactory();_=factory.CreateClient();await using var scope=factory.Services.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<ITimeZoneService>();
        var sao=service.ParseTenantInstant("2026-09-15T14:00:00-03:00","America/Sao_Paulo");
        Assert.Equal(new DateTimeOffset(2026,9,15,17,0,0,TimeSpan.Zero),sao);
        Assert.Equal(TimeSpan.FromHours(-3),service.UtcToTenant(sao,"America/Sao_Paulo").Offset);
        var lisbon=service.ParseTenantInstant("2026-09-15T14:00:00+01:00","Europe/Lisbon");
        Assert.NotEqual(sao,lisbon);
        Assert.Throws<SchedulingValidationException>(()=>service.ParseTenantInstant("2026-03-08T02:30:00-05:00","America/New_York"));
        Assert.Throws<SchedulingValidationException>(()=>service.ParseTenantInstant("2026-11-01T01:30:00-03:00","America/New_York"));
        Assert.NotEqual(service.ParseTenantInstant("2026-11-01T01:30:00-04:00","America/New_York"),service.ParseTenantInstant("2026-11-01T01:30:00-05:00","America/New_York"));
        Assert.Throws<SchedulingValidationException>(()=>service.ParseTenantInstant("2026-09-15T14:00:00Z","America/Sao_Paulo"));
    }

    [Fact]
    public async Task CreatesWithServiceDurationAndRejectsConflictBlockUnavailableAndCrossTenantIdor()
    {
        await using var factory=new ApiFactory();var a=factory.CreateClient();var b=factory.CreateClient();var globalA=await Register(a,"schedule-a@nexora.test");var globalB=await Register(b,"schedule-b@nexora.test");
        await CreateTenant(a,globalA,"schedule-a","America/Sao_Paulo");await CreateTenant(b,globalB,"schedule-b","America/New_York");a.DefaultRequestHeaders.Authorization=new("Bearer",await Select(a,globalA,"schedule-a"));b.DefaultRequestHeaders.Authorization=new("Bearer",await Select(b,globalB,"schedule-b"));
        var customer=await Post<IdDto>(a,"/api/v1/customers",new{name="Cliente",phone="1",email="c@test.local",birthDate=(string?)null,notes=""});var professional=await Post<IdDto>(a,"/api/v1/professionals",new{name="Ana",email="a@test.local",phone="1",isActive=true});var service=await Post<IdDto>(a,"/api/v1/services",new{name="Consulta",description="",durationMinutes=30,price=10,isActive=true});(await a.PutAsync($"/api/v1/professionals/{professional.Id}/services/{service.Id}",null)).EnsureSuccessStatusCode();
        (await a.PutAsJsonAsync("/api/v1/working-hours",new{professionalId=professional.Id,dayOfWeek="Tuesday",startLocal="09:00:00",endLocal="18:00:00"})).EnsureSuccessStatusCode();
        var appointment=await Post<AppointmentDto>(a,"/api/v1/appointments",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T14:00:00-03:00",timeZoneId="Europe/Lisbon",notes="Teste"});
        Assert.Equal(TimeSpan.FromMinutes(30),appointment.EndAt-appointment.StartAt);Assert.Equal("2026-09-15T14:00:00-03:00",appointment.StartAtLocal);
        Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T14:15:00-03:00",notes=""})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T08:00:00-03:00",notes=""})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T16:00:00Z",notes=""})).StatusCode);
        var block=await Post<IdDto>(a,"/api/v1/blocked-periods",new{professionalId=professional.Id,startAt="2026-09-15T15:00:00-03:00",endAt="2026-09-15T16:00:00-03:00",reason="Pausa"});
        Assert.Equal(HttpStatusCode.Conflict,(await a.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T15:15:00-03:00",notes=""})).StatusCode);
        Assert.Empty((await b.GetFromJsonAsync<AppointmentDto[]>("/api/v1/appointments?from=2026-09-15T00:00:00Z&to=2026-09-16T00:00:00Z"))!);
        Assert.Equal(HttpStatusCode.NotFound,(await b.PutAsJsonAsync($"/api/v1/appointments/{appointment.Id}",new{customerId=customer.Id,professionalId=professional.Id,serviceId=service.Id,startAt="2026-09-15T16:00:00-04:00",notes="IDOR"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.DeleteAsync($"/api/v1/blocked-periods/{block.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await a.PatchAsJsonAsync($"/api/v1/appointments/{appointment.Id}/cancel",new{})).StatusCode);
    }

    [Fact]
    public async Task TenantCreationRequiresValidIanaTimeZone()
    {await using var factory=new ApiFactory();var client=factory.CreateClient();var token=await Register(client,"timezone@nexora.test");client.DefaultRequestHeaders.Authorization=new("Bearer",token);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/tenants",new{name="Missing",slug="missing-zone"})).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/tenants",new{name="Invalid",slug="invalid-zone",timeZoneId="Mars/Olympus"})).StatusCode);}
    private static async Task<T> Post<T>(HttpClient c,string uri,object body){var r=await c.PostAsJsonAsync(uri,body);r.EnsureSuccessStatusCode();return(await r.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<string> Register(HttpClient c,string email){var r=await c.PostAsJsonAsync("/api/v1/identity/register",new{email,password="Correct-Horse-42"});r.EnsureSuccessStatusCode();SetCookie(c,r);return(await r.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;}
    private static async Task CreateTenant(HttpClient c,string token,string slug,string zone){c.DefaultRequestHeaders.Authorization=new("Bearer",token);(await c.PostAsJsonAsync("/api/v1/tenants",new{name=slug,slug,timeZoneId=zone})).EnsureSuccessStatusCode();}
    private static async Task<string> Select(HttpClient c,string token,string slug){c.DefaultRequestHeaders.Authorization=new("Bearer",token);var r=await c.PostAsJsonAsync($"/api/v1/t/{slug}/session",new{});r.EnsureSuccessStatusCode();c.DefaultRequestHeaders.Remove("Cookie");SetCookie(c,r);return(await r.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;}
    private static void SetCookie(HttpClient c,HttpResponseMessage r)=>c.DefaultRequestHeaders.Add("Cookie",r.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("nexora_refresh=",StringComparison.Ordinal)).Split(';',2)[0]);
    private sealed record TokenDto(string AccessToken);private sealed record IdDto(Guid Id);private sealed record AppointmentDto(Guid Id,DateTimeOffset StartAt,DateTimeOffset EndAt,string StartAtLocal);
}
