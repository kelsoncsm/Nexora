using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Catalog;
using Nexora.Domain.Customers;
using Nexora.Domain.Scheduling;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

// Reproduces the reports/overview 500: the productivity query only fails to translate on the
// real Npgsql provider (the in-memory provider client-evaluates it). Gated by
// NEXORA_HARDENING_POSTGRES, matching PostgresConcurrencyTests.
[Collection("Postgres")]
public sealed class ReportsProductivityPostgresTests
{
    [Fact]
    public async Task TenantOverviewRanksProductivityAndSucceedsOnPostgres()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        var key = Guid.NewGuid().ToString("N");
        var slug = $"pg-reports-{key}";
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var token = await Register(client, $"{slug}@nexora.test");
        var tenantId = await CreateTenant(client, token, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Select(client, token, slug));

        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(-1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var customer = new Customer(tenantId, "Cliente", "1", "c@test.local", null, null, now);
            var service = new Service(tenantId, "Corte", null, 30, 45m);
            var ana = new Professional(tenantId, "Ana", "ana@test.local", "1");
            var bruno = new Professional(tenantId, "Bruno", "bruno@test.local", "2");
            db.AddRange(customer, service, ana, bruno);

            Appointment Completed(Professional professional, int hourOffset)
            {
                var appointment = new Appointment(tenantId, customer.Id, professional.Id, service.Id,
                    start.AddHours(hourOffset), start.AddHours(hourOffset).AddMinutes(30), null, now);
                appointment.ChangeStatus(AppointmentStatus.Completed, now);
                return appointment;
            }

            db.AddRange(Completed(ana, 1), Completed(ana, 2), Completed(bruno, 3));
            db.Add(new Appointment(tenantId, customer.Id, bruno.Id, service.Id,
                start.AddHours(4), start.AddHours(4).AddMinutes(30), null, now)); // Scheduled, excluded from productivity
            await db.SaveChangesAsync();
        }

        var from = Uri.EscapeDataString(now.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(now.AddDays(7).ToString("O"));
        var response = await client.GetAsync($"/api/v1/reports/overview?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<TenantReportResponse>())!;
        Assert.Equal(4, body.Appointments.Total);
        Assert.Equal(3, body.Appointments.Completed);
        Assert.Equal(2, body.Productivity.Count);
        Assert.Equal("Ana", body.Productivity[0].ProfessionalName);
        Assert.Equal(2, body.Productivity[0].CompletedAppointments);
        Assert.Equal("Bruno", body.Productivity[1].ProfessionalName);
        Assert.Equal(1, body.Productivity[1].CompletedAppointments);
    }

    [Fact]
    public async Task TenantOverviewWithoutDataReturnsZeroesOnPostgres()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        var key = Guid.NewGuid().ToString("N");
        var slug = $"pg-reports-empty-{key}";
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var token = await Register(client, $"{slug}@nexora.test");
        await CreateTenant(client, token, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Select(client, token, slug));

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("O"));
        var response = await client.GetAsync($"/api/v1/reports/overview?from={from}&to={to}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<TenantReportResponse>())!;
        Assert.Equal(0, body.Customers);
        Assert.Equal(0, body.Appointments.Total);
        Assert.Equal(0, body.Appointments.Completed);
        Assert.Empty(body.Productivity);
    }

    private static async Task<string> Register(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", RefreshCookie(response));
        return (await response.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;
    }

    private static async Task<Guid> CreateTenant(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "America/Sao_Paulo" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static async Task<string> Select(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", RefreshCookie(response));
        return (await response.Content.ReadFromJsonAsync<TokenDto>())!.AccessToken;
    }

    private static string RefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0];

    private sealed record TokenDto(string AccessToken);
    private sealed record IdDto(Guid Id);
    private sealed record TenantReportResponse(int Customers, AppointmentMetricsResponse Appointments, IReadOnlyList<ProductivityResponse> Productivity);
    private sealed record AppointmentMetricsResponse(int Total, int Completed, int Cancelled, int NoShow);
    private sealed record ProductivityResponse(Guid ProfessionalId, string ProfessionalName, int CompletedAppointments);
}
