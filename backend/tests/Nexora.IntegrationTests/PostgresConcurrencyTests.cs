using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;
using Nexora.Application.Identity;

namespace Nexora.IntegrationTests;

[Collection("Postgres")]
public sealed class PostgresConcurrencyTests
{
    [Fact]
    public async Task AppointmentCreationIsAtomicPerTenantAndProfessional()
    {
        if(Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null)return;
        var key=Guid.NewGuid().ToString("N");var slug=$"pg-schedule-{key}";await using var factory=new PostgresApiFactory();await factory.InitializeAsync();using var client=factory.CreateClient();var token=await Register(client,$"{slug}@nexora.test");var tenantId=await CreateTenant(client,token,slug);await TestFeatureCatalog.GrantAllModulesToTenantAsync(factory,tenantId);client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",await Select(client,token,slug));
        var customer=await Post<IdDto>(client,"/api/v1/customers",new{name="Cliente",phone="1",email=$"{key}@customer.test",notes=""});var service=await Post<IdDto>(client,"/api/v1/services",new{name="Corte",description="",durationMinutes=30,price=45,isActive=true});var first=await CreateProfessional(client,$"Primeiro-{key}",service.Id);var second=await CreateProfessional(client,$"Segundo-{key}",service.Id);
        var body=new{customerId=customer.Id,professionalId=first.Id,serviceId=service.Id,startAt="2026-09-15T10:00:00Z",notes="concorrente"};var responses=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>client.PostAsJsonAsync("/api/v1/appointments",body)));Assert.Equal(1,responses.Count(x=>x.StatusCode==HttpStatusCode.Created));Assert.Equal(9,responses.Count(x=>x.StatusCode==HttpStatusCode.Conflict));
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();Assert.Equal(1,await db.Appointments.CountAsync(x=>x.ProfessionalId==first.Id&&x.StartAt==new DateTimeOffset(2026,9,15,10,0,0,TimeSpan.Zero)));}
        var distinct=await Task.WhenAll(client.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=first.Id,serviceId=service.Id,startAt="2026-09-15T11:00:00Z",notes=""}),client.PostAsJsonAsync("/api/v1/appointments",new{customerId=customer.Id,professionalId=second.Id,serviceId=service.Id,startAt="2026-09-15T11:00:00Z",notes=""}));Assert.All(distinct,x=>Assert.Equal(HttpStatusCode.Created,x.StatusCode));
    }

    [Fact]
    public async Task CheckoutConvergesToOneTrialCommercialObligation()
    {
        if(Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null)return;
        var key=Guid.NewGuid().ToString("N");var slug=$"pg-checkout-{key}";await using var factory=new PostgresApiFactory();await factory.InitializeAsync();using var trialClient=factory.CreateClient();var token=await Register(trialClient,$"{slug}@nexora.test");var tenantId=await CreateTenant(trialClient,token,slug);Guid subscriptionId;DateTimeOffset trialEnd;
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var now=DateTimeOffset.UtcNow;var plan=new Plan($"PG-{key}","PG Checkout",now);var subscription=new Subscription(tenantId,plan.Id,BillingInterval.Monthly,now,TimeSpan.FromDays(14));db.AddRange(plan,subscription,new PlanPrice(plan.Id,BillingInterval.Monthly,"BRL",99.90m,now));await db.SaveChangesAsync();await db.Entry(subscription).ReloadAsync();subscriptionId=subscription.Id;trialEnd=subscription.TrialEndAt;}
        factory.PaymentGateway.CheckoutResult=new($"pg-trial-{key}",BillingPaymentStatus.Pending,null,null,DateTimeOffset.UtcNow);trialClient.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",await Select(trialClient,token,slug));var responses=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>trialClient.PostAsJsonAsync("/api/v1/billing/checkout",new{paymentMethod="Pix",payerEmail=$"{slug}@nexora.test"})));Assert.All(responses,x=>Assert.Equal(HttpStatusCode.OK,x.StatusCode));
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var invoice=await db.BillingInvoices.SingleAsync(x=>x.SubscriptionId==subscriptionId);Assert.Equal(trialEnd,invoice.CoverageStart);Assert.Equal(trialEnd.AddMonths(1),invoice.CoverageEnd);Assert.Equal(SubscriptionStatus.Trialing,(await db.Subscriptions.SingleAsync(x=>x.Id==subscriptionId)).Status);Assert.Single(await db.BillingPayments.Where(x=>x.BillingInvoiceId==invoice.Id).ToArrayAsync());}
        Assert.Equal(1,factory.PaymentGateway.CreateCalls);Assert.Single(factory.PaymentGateway.Requests.Select(x=>x.IdempotencyKey).Distinct());
    }

    [Fact]
    public async Task ActiveCheckoutUsesAndReusesTheNextCommercialPeriod()
    {
        if(Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null)return;
        var key=Guid.NewGuid().ToString("N");var slug=$"pg-active-{key}";await using var factory=new PostgresApiFactory();await factory.InitializeAsync();using var activeClient=factory.CreateClient();var activeToken=await Register(activeClient,$"{slug}@nexora.test");var activeTenant=await CreateTenant(activeClient,activeToken,slug);Guid activeSubscriptionId;DateTimeOffset currentEnd;
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var now=DateTimeOffset.UtcNow;var plan=new Plan($"PGA-{key}","PG Active",now);var subscription=new Subscription(activeTenant,plan.Id,BillingInterval.Yearly,now,TimeSpan.FromDays(14));subscription.Activate(now);db.AddRange(plan,subscription,new PlanPrice(plan.Id,BillingInterval.Yearly,"BRL",999m,now));await db.SaveChangesAsync();await db.Entry(subscription).ReloadAsync();activeSubscriptionId=subscription.Id;currentEnd=subscription.CurrentPeriodEnd;}
        factory.PaymentGateway.CheckoutResult=new($"pg-active-{key}",BillingPaymentStatus.Pending,null,null,DateTimeOffset.UtcNow);activeClient.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",await Select(activeClient,activeToken,slug));Assert.Equal(HttpStatusCode.OK,(await activeClient.PostAsJsonAsync("/api/v1/billing/checkout",new{paymentMethod="Pix",payerEmail=$"{slug}@nexora.test"})).StatusCode);Assert.Equal(HttpStatusCode.OK,(await activeClient.PostAsJsonAsync("/api/v1/billing/checkout",new{paymentMethod="Pix",payerEmail=$"{slug}@nexora.test"})).StatusCode);
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var invoice=await db.BillingInvoices.SingleAsync(x=>x.SubscriptionId==activeSubscriptionId);Assert.Equal(currentEnd,invoice.CoverageStart);Assert.Equal(currentEnd.AddYears(1),invoice.CoverageEnd);}
        Assert.Equal(1,factory.PaymentGateway.CreateCalls);
    }

    [Fact]
    public async Task RefreshTokenCanBeConsumedOnlyOnceUnderConcurrency()
    {
        if(Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null)return;
        var email=$"pg-refresh-{Guid.NewGuid():N}@nexora.test";await using var factory=new PostgresApiFactory();await factory.InitializeAsync();string cookie;await using(var registrationScope=factory.Services.CreateAsyncScope()){var session=await registrationScope.ServiceProvider.GetRequiredService<IIdentityService>().RegisterAsync(new RegisterCommand(email,"Correct-Horse-42"),CancellationToken.None);cookie=$"nexora_refresh={session.RefreshToken}";}
        var clients=Enumerable.Range(0,10).Select(_=>factory.CreateClient()).ToArray();foreach(var client in clients)client.DefaultRequestHeaders.Add("Cookie",cookie);var responses=await Task.WhenAll(clients.Select(x=>x.PostAsJsonAsync("/api/v1/identity/refresh",new{})));foreach(var client in clients)client.Dispose();Assert.Equal(1,responses.Count(x=>x.StatusCode==HttpStatusCode.OK));Assert.Equal(9,responses.Count(x=>x.StatusCode==HttpStatusCode.Unauthorized));
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var normalized=email.ToUpperInvariant();var userId=await db.Users.Where(x=>x.NormalizedEmail==normalized).Select(x=>x.Id).SingleAsync();var tokens=await db.RefreshTokens.Where(x=>x.UserId==userId).ToArrayAsync();Assert.Equal(2,tokens.Length);Assert.Single(tokens,x=>x.RevokedAt is null);Assert.Single(tokens,x=>x.RevokedAt is not null&&x.ReplacedByTokenHash is not null);
    }

    private static async Task<IdDto> CreateProfessional(HttpClient client,string name,Guid serviceId){var value=await Post<IdDto>(client,"/api/v1/professionals",new{name,email=$"{name.ToLowerInvariant()}@test.local",phone="1",isActive=true});(await client.PutAsync($"/api/v1/professionals/{value.Id}/services/{serviceId}",null)).EnsureSuccessStatusCode();(await client.PutAsJsonAsync("/api/v1/working-hours",new{professionalId=value.Id,dayOfWeek="Tuesday",startLocal="08:00:00",endLocal="18:00:00"})).EnsureSuccessStatusCode();return value;}
    private static async Task<T> Post<T>(HttpClient client,string uri,object body){var response=await client.PostAsJsonAsync(uri,body);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<string> Register(HttpClient client,string email){var response=await client.PostAsJsonAsync("/api/v1/identity/register",new{email,password="Correct-Horse-42"});response.EnsureSuccessStatusCode();client.DefaultRequestHeaders.Add("Cookie",RefreshCookie(response));return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;}
    private static async Task<Guid> CreateTenant(HttpClient client,string token,string slug){client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);var response=await client.PostAsJsonAsync("/api/v1/tenants",new{name=slug,slug,timeZoneId="UTC"});response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<IdDto>())!.Id;}
    private static async Task<string> Select(HttpClient client,string token,string slug){client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);var response=await client.PostAsJsonAsync($"/api/v1/t/{slug}/session",new{});response.EnsureSuccessStatusCode();client.DefaultRequestHeaders.Remove("Cookie");client.DefaultRequestHeaders.Add("Cookie",RefreshCookie(response));return(await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;}
    private static string RefreshCookie(HttpResponseMessage response)=>response.Headers.GetValues("Set-Cookie").Single(x=>x.StartsWith("nexora_refresh=",StringComparison.Ordinal)).Split(';',2)[0];
    private sealed record Token(string AccessToken);private sealed record IdDto(Guid Id);
}
