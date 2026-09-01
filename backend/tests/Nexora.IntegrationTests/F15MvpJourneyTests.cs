using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Administration;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class F15MvpJourneyTests
{
    [Fact]
    public async Task BarbershopSalonJourneyIntegratesOnboardingVerticalAndOperationalCore()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var catalog = await SeedCatalog(factory);

        var globalToken = await Register(client, "f15-owner@nexora.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", globalToken);

        var segments = await client.GetFromJsonAsync<SegmentOption[]>("/api/v1/onboarding/segments");
        var segment = Assert.Single(segments!, x => x.Code == "BARBERSHOP_SALON");
        Assert.Equal(catalog.SegmentId, segment.Id);
        var plans = await client.GetFromJsonAsync<PlanOption[]>("/api/v1/onboarding/plans");
        var plan = Assert.Single(plans!, x => x.Code == "MVP_TRIAL");
        Assert.Equal(catalog.PlanId, plan.PlanId);

        var draft = await Post<Draft>(client, "/api/v1/onboarding/drafts", new { });
        _ = await Put<Draft>(client, $"/api/v1/onboarding/drafts/{draft.Id}", new
        {
            currentStep = 5,
            companyName = "Barbearia F15",
            companySlug = "barbearia-f15",
            segmentId = segment.Id,
            planId = plan.PlanId,
            billingInterval = "Monthly",
            timeZoneId = "America/Sao_Paulo"
        });
        var completion = await Post<Completion>(client, $"/api/v1/onboarding/drafts/{draft.Id}/complete", new { });

        var publicTenant = await client.GetFromJsonAsync<PublicTenant>($"/api/v1/t/{completion.TenantSlug}");
        Assert.Equal(completion.TenantId, publicTenant!.Id);
        Assert.Equal("America/Sao_Paulo", publicTenant.TimeZoneId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await SelectTenant(client, globalToken, completion.TenantSlug));

        var subscription = await client.GetFromJsonAsync<Subscription>("/api/v1/subscription");
        Assert.Equal(completion.TenantId, subscription!.TenantId);
        Assert.Equal(catalog.PlanId, subscription.PlanId);
        Assert.Equal("MVP_TRIAL", subscription.PlanCode);
        Assert.Equal("Trialing", subscription.Status);

        var setup = await client.GetFromJsonAsync<VerticalSetup>("/api/v1/vertical-setup");
        Assert.Equal("BARBERSHOP_SALON", setup!.Code);
        Assert.Contains(setup.ServicePresets, x => x.Code == "HAIRCUT" && x.Category == "Cabelo");

        var forgedTenantId = Guid.NewGuid();
        var preset = new
        {
            tenantId = forgedTenantId,
            services = new[] { new { presetCode = "HAIRCUT", durationMinutes = 40, price = 55m } }
        };
        (await client.PostAsJsonAsync("/api/v1/vertical-setup/apply", preset)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/vertical-setup/apply", preset)).EnsureSuccessStatusCode();
        var services = await client.GetFromJsonAsync<Service[]>("/api/v1/services");
        var service = Assert.Single(services!);
        Assert.Equal("Corte", service.Name);
        Assert.Equal(40, service.DurationMinutes);
        Assert.Equal(55m, service.Price);

        var professional = await Post<Professional>(client, "/api/v1/professionals", new
        {
            tenantId = forgedTenantId, name = "Ana", email = "ana@f15.test", phone = "11999990000", isActive = true
        });
        (await client.PutAsync($"/api/v1/professionals/{professional.Id}/services/{service.Id}", null)).EnsureSuccessStatusCode();
        var linkedProfessional = await client.GetFromJsonAsync<Professional>($"/api/v1/professionals/{professional.Id}");
        Assert.Contains(service.Id, linkedProfessional!.ServiceIds);

        var customer = await Post<Customer>(client, "/api/v1/customers", new
        {
            tenantId = forgedTenantId, name = "Cliente F15", phone = "11888880000", email = "cliente@f15.test",
            birthDate = (string?)null, notes = "Jornada MVP"
        });

        _ = await Put<WorkingHours>(client, "/api/v1/working-hours", new
        {
            tenantId = forgedTenantId, professionalId = professional.Id, dayOfWeek = "Tuesday",
            startLocal = "09:00:00", endLocal = "18:00:00"
        });
        var availability = await client.GetFromJsonAsync<WorkingHours[]>(
            $"/api/v1/working-hours?professionalId={professional.Id}");
        Assert.Contains(availability!, x => x.ProfessionalId == professional.Id
            && x.StartLocal == new TimeOnly(9, 0) && x.EndLocal == new TimeOnly(18, 0));

        var appointment = await Post<Appointment>(client, "/api/v1/appointments", new
        {
            tenantId = forgedTenantId, customerId = customer.Id, professionalId = professional.Id,
            serviceId = service.Id, startAt = "2026-09-15T14:00:00-03:00", notes = "Corte F15"
        });
        Assert.Equal(customer.Id, appointment.CustomerId);
        Assert.Equal(professional.Id, appointment.ProfessionalId);
        Assert.Equal(service.Id, appointment.ServiceId);
        Assert.Equal(TimeSpan.FromMinutes(40), appointment.EndAt - appointment.StartAt);

        var agenda = await client.GetFromJsonAsync<Appointment[]>(
            "/api/v1/appointments?from=2026-09-15T00:00:00Z&to=2026-09-16T00:00:00Z");
        Assert.Contains(agenda!, x => x.Id == appointment.Id);
        var finalSubscription = await client.GetFromJsonAsync<Subscription>("/api/v1/subscription");
        Assert.Equal(subscription, finalSubscription);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(completion.TenantId, (await db.Services.SingleAsync(x => x.Id == service.Id)).TenantId);
        Assert.Equal(completion.TenantId, (await db.Professionals.SingleAsync(x => x.Id == professional.Id)).TenantId);
        Assert.Equal(completion.TenantId, (await db.Customers.SingleAsync(x => x.Id == customer.Id)).TenantId);
        Assert.Equal(completion.TenantId, (await db.Appointments.SingleAsync(x => x.Id == appointment.Id)).TenantId);
        Assert.False(await db.Services.AnyAsync(x => x.TenantId == forgedTenantId));
    }

    private static async Task<(Guid SegmentId, Guid PlanId)> SeedCatalog(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var now = DateTimeOffset.UtcNow;
        var segment = new BusinessSegment("BARBERSHOP_SALON", "Barbearia / Salão", now);
        var plan = new Plan("MVP_TRIAL", "MVP Trial", now);
        plan.ConfigureCommercialAvailability(true, true);
        db.AddRange(segment, plan, new PlanPrice(plan.Id, BillingInterval.Monthly, "BRL", 49.90m, now));
        await db.SaveChangesAsync();
        return (segment.Id, plan.Id);
    }

    private static async Task<string> Register(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode();
        SetRefreshCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static async Task<string> SelectTenant(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        SetRefreshCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static void SetRefreshCookie(HttpClient client, HttpResponseMessage response) =>
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);

    private static async Task<T> Post<T>(HttpClient client, string uri, object body)
    {
        var response = await client.PostAsJsonAsync(uri, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Put<T>(HttpClient client, string uri, object body)
    {
        var response = await client.PutAsJsonAsync(uri, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed record Token(string AccessToken);
    private sealed record SegmentOption(Guid Id, string Code);
    private sealed record PlanOption(Guid PlanId, string Code);
    private sealed record Draft(Guid Id);
    private sealed record Completion(Guid TenantId, string TenantSlug, Guid SubscriptionId);
    private sealed record PublicTenant(Guid Id, string TimeZoneId);
    private sealed record VerticalSetup(string Code, Preset[] ServicePresets);
    private sealed record Preset(string Code, string Category);
    private sealed record Service(Guid Id, string Name, int DurationMinutes, decimal Price);
    private sealed record Professional(Guid Id, Guid[] ServiceIds);
    private sealed record Customer(Guid Id);
    private sealed record WorkingHours(Guid Id, Guid ProfessionalId, TimeOnly StartLocal, TimeOnly EndLocal);
    private sealed record Appointment(Guid Id, Guid CustomerId, Guid ProfessionalId, Guid ServiceId, DateTimeOffset StartAt, DateTimeOffset EndAt);
    private sealed record Subscription(Guid Id, Guid TenantId, Guid PlanId, string PlanCode, string Status);
}
