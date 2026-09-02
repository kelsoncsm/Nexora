using Nexora.Application.Scheduling;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Scheduling;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/scheduling/context", GetContextAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsRead)
            .WithTags("Scheduling");

        endpoints.MapWorkingHoursEndpoints();
        endpoints.MapBlockedPeriodEndpoints();
        endpoints.MapAppointmentEndpoints();

        return endpoints;
    }

    private static async Task<IResult> GetContextAsync(
        ITenantContext tenant, ITenantTimeZoneProvider zones, CancellationToken ct) =>
        Results.Ok(new { timeZoneId = await zones.GetTimeZoneIdAsync(tenant.TenantId, ct) });
}
