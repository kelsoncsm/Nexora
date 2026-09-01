using Nexora.Application.Scheduling;
using Nexora.Application.Tenancy;
using Nexora.Domain.Scheduling;

namespace Nexora.Api.Scheduling;

internal static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var appointments = endpoints.MapGroup("/api/v1/appointments").WithTags("Scheduling");

        appointments.MapGet(
                "/",
                (DateTimeOffset from, DateTimeOffset to, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
                    service.GetAppointmentsAsync(tenant.TenantId, from, to, ct))
            .RequireAuthorization(TenantPermissions.AppointmentsRead);

        appointments.MapPost("/", CreateAppointmentAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsCreate);

        appointments.MapPut("/{id:guid}", UpdateAppointmentAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsUpdate);

        appointments.MapPatch("/{id:guid}/status", ChangeStatusAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsUpdate);

        appointments.MapPatch("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization(TenantPermissions.AppointmentsCancel);
    }

    private static async Task<IResult> CreateAppointmentAsync(
        AppointmentInput input, ITenantContext tenant, ISchedulingService service, CancellationToken ct)
    {
        var created = await service.CreateAppointmentAsync(tenant.TenantId, input, ct);
        return Results.Created("/api/v1/appointments", created);
    }

    private static async Task<IResult> UpdateAppointmentAsync(
        Guid id, AppointmentInput input, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
        await service.UpdateAppointmentAsync(tenant.TenantId, id, input, ct) is { } value
            ? Results.Ok(value)
            : Results.NotFound();

    private static async Task<IResult> ChangeStatusAsync(
        Guid id, AppointmentStatusInput input, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
        await service.ChangeStatusAsync(tenant.TenantId, id, input.Status, ct) is { } value
            ? Results.Ok(value)
            : Results.NotFound();

    private static async Task<IResult> CancelAsync(
        Guid id, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
        await service.ChangeStatusAsync(tenant.TenantId, id, AppointmentStatus.Cancelled, ct) is { } value
            ? Results.Ok(value)
            : Results.NotFound();
}
