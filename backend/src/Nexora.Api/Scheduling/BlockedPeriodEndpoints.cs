using Nexora.Application.Scheduling;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Scheduling;

internal static class BlockedPeriodEndpoints
{
    public static void MapBlockedPeriodEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var blocks = endpoints.MapGroup("/api/v1/blocked-periods").WithTags("Scheduling");

        blocks.MapGet(
                "/",
                (DateTimeOffset from, DateTimeOffset to, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
                    service.GetBlockedPeriodsAsync(tenant.TenantId, from, to, ct))
            .RequireAuthorization(TenantPermissions.AppointmentsRead);

        blocks.MapPost("/", CreateBlockedPeriodAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsUpdate);

        blocks.MapDelete("/{id:guid}", DeleteBlockedPeriodAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsUpdate);
    }

    private static async Task<IResult> CreateBlockedPeriodAsync(
        BlockedPeriodInput input, ITenantContext tenant, ISchedulingService service, CancellationToken ct)
    {
        var created = await service.CreateBlockedPeriodAsync(tenant.TenantId, input, ct);
        return Results.Created("/api/v1/blocked-periods", created);
    }

    private static async Task<IResult> DeleteBlockedPeriodAsync(
        Guid id, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
        await service.DeleteBlockedPeriodAsync(tenant.TenantId, id, ct)
            ? Results.NoContent()
            : Results.NotFound();
}
