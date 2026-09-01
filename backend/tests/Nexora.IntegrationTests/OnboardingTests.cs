using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Administration;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class OnboardingTests
{
    [Fact]
    public async Task CompletionIsUserScopedAtomicIdempotentAndStartsTrialWithoutPayment()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var owner = factory.CreateClient(); using var other = factory.CreateClient();
        var ownerToken = await Register(owner, "onboarding-owner@nexora.test");
        var otherToken = await Register(other, "onboarding-other@nexora.test");
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var catalog = await SeedCatalog(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/onboarding/drafts", new { })).StatusCode);
        var plans = await owner.GetFromJsonAsync<PlanOption[]>("/api/v1/onboarding/plans");
        Assert.Single(plans!); Assert.Equal(49.90m, plans![0].Amount); Assert.Contains(plans[0].Limits, x => x.FeatureCode == "CUSTOMERS" && x.Limit == 100);
        var segments = await owner.GetFromJsonAsync<SegmentOption[]>("/api/v1/onboarding/segments"); Assert.Single(segments!);

        var started = await Post<DraftDto>(owner, "/api/v1/onboarding/drafts", new { });
        var resumed = await Post<DraftDto>(owner, "/api/v1/onboarding/drafts", new { }); Assert.Equal(started.Id, resumed.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/onboarding/drafts/{started.Id}")).StatusCode);
        var updated = await Put<DraftDto>(owner, $"/api/v1/onboarding/drafts/{started.Id}", new
        {
            currentStep = 5, companyName = "Nexora Studio", companySlug = "nexora-studio", segmentId = catalog.SegmentId,
            planId = catalog.PlanId, billingInterval = "Monthly", timeZoneId = "America/Sao_Paulo", amount = 0.01m
        });
        Assert.Equal(5, updated.CurrentStep);

        var completion = await Post<CompletionDto>(owner, $"/api/v1/onboarding/drafts/{started.Id}/complete", new { });
        var duplicate = await Post<CompletionDto>(owner, $"/api/v1/onboarding/drafts/{started.Id}/complete", new { });
        Assert.Equal(completion.TenantId, duplicate.TenantId); Assert.Equal(completion.SubscriptionId, duplicate.SubscriptionId);
        Assert.Equal(14, (completion.TrialEndAt - DateTimeOffset.UtcNow).TotalDays, 1);
        Assert.Equal(0, factory.PaymentGateway.CreateCalls);

        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var tenant = await db.Tenants.Include(x => x.Users).Include(x => x.Roles).ThenInclude(x => x.Permissions).SingleAsync(x => x.Id == completion.TenantId);
        Assert.Equal(catalog.SegmentId, tenant.SegmentId); Assert.Single(tenant.Users); Assert.Single(tenant.Roles); Assert.NotEmpty(tenant.Roles.Single().Permissions);
        var subscription = await db.Subscriptions.SingleAsync(x => x.Id == completion.SubscriptionId);
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status); Assert.Equal(catalog.PlanId, subscription.PlanId);
        Assert.Equal(1, await db.Tenants.CountAsync(x => x.Slug == "nexora-studio"));
    }

    [Fact]
    public async Task InvalidOrUnavailableInputCreatesNoPartialTenant()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider(); using var client = factory.CreateClient();
        var token = await Register(client, "onboarding-invalid@nexora.test"); client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var catalog = await SeedCatalog(factory); var draft = await Post<DraftDto>(client, "/api/v1/onboarding/drafts", new { });
        await Put<DraftDto>(client, $"/api/v1/onboarding/drafts/{draft.Id}", new { currentStep=5,companyName="Invalid",companySlug="INVALID SLUG",segmentId=catalog.SegmentId,planId=catalog.PlanId,billingInterval="Monthly",timeZoneId="America/Sao_Paulo" });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/onboarding/drafts/{draft.Id}/complete", new { })).StatusCode);
        await using var scope=factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>(); Assert.Empty(await db.Tenants.ToArrayAsync()); Assert.Empty(await db.Subscriptions.ToArrayAsync());
    }

    private static async Task<(Guid SegmentId, Guid PlanId)> SeedCatalog(ApiFactory factory)
    {
        await using var scope=factory.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var now=DateTimeOffset.UtcNow;
        var segment=new BusinessSegment("BEAUTY","Beleza",now);var hidden=new BusinessSegment("HIDDEN","Oculto",now);hidden.Update("Oculto",false);
        var plan=new Plan("START","Start",now);plan.ConfigureCommercialAvailability(true,true);var privatePlan=new Plan("PRIVATE","Private",now);
        var feature=new Feature("CUSTOMERS","Clientes",now);plan.Features.Add(new PlanFeature(plan.Id,feature.Id,true,100));
        db.AddRange(segment,hidden,plan,privatePlan,feature,new PlanPrice(plan.Id,BillingInterval.Monthly,"BRL",49.90m,now),new PlanPrice(privatePlan.Id,BillingInterval.Monthly,"BRL",1m,now));await db.SaveChangesAsync();return(segment.Id,plan.Id);
    }
    private static async Task<string> Register(HttpClient client,string email){var response=await client.PostAsJsonAsync("/api/v1/identity/register",new{email,password="Correct-Horse-42"});response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;}
    private static async Task<T> Post<T>(HttpClient client,string uri,object body){var response=await client.PostAsJsonAsync(uri,body);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    private static async Task<T> Put<T>(HttpClient client,string uri,object body){var response=await client.PutAsJsonAsync(uri,body);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    private sealed record TokenDto(string AccessToken);private sealed record DraftDto(Guid Id,int CurrentStep);private sealed record CompletionDto(Guid TenantId,Guid SubscriptionId,DateTimeOffset TrialEndAt);
    private sealed record SegmentOption(Guid Id,string Code,string Name);private sealed record FeatureLimit(string FeatureCode,int? Limit);private sealed record PlanOption(Guid PlanId,decimal Amount,IReadOnlyList<FeatureLimit> Limits);
}
