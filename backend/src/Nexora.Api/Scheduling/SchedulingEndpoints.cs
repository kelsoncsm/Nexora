using Nexora.Application.Scheduling;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Scheduling;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/scheduling/context",async(ITenantContext tenant,ITenantTimeZoneProvider zones,CancellationToken ct)=>Results.Ok(new{timeZoneId=await zones.GetTimeZoneIdAsync(tenant.TenantId,ct)})).RequireAuthorization(TenantPermissions.AppointmentsRead).WithTags("Scheduling");
        var working=endpoints.MapGroup("/api/v1/working-hours").WithTags("Scheduling");
        working.MapGet("/",(Guid? professionalId,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>service.GetWorkingHoursAsync(tenant.TenantId,professionalId,ct)).RequireAuthorization(TenantPermissions.AppointmentsRead);
        working.MapPut("/",(WorkingHoursInput input,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>service.SetWorkingHoursAsync(tenant.TenantId,input,ct)).RequireAuthorization(TenantPermissions.AppointmentsUpdate);
        var blocks=endpoints.MapGroup("/api/v1/blocked-periods").WithTags("Scheduling");
        blocks.MapGet("/",(DateTimeOffset from,DateTimeOffset to,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>service.GetBlockedPeriodsAsync(tenant.TenantId,from,to,ct)).RequireAuthorization(TenantPermissions.AppointmentsRead);
        blocks.MapPost("/",async(BlockedPeriodInput input,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>Results.Created("/api/v1/blocked-periods",await service.CreateBlockedPeriodAsync(tenant.TenantId,input,ct))).RequireAuthorization(TenantPermissions.AppointmentsUpdate);
        blocks.MapDelete("/{id:guid}",async(Guid id,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>await service.DeleteBlockedPeriodAsync(tenant.TenantId,id,ct)?Results.NoContent():Results.NotFound()).RequireAuthorization(TenantPermissions.AppointmentsUpdate);
        var appointments=endpoints.MapGroup("/api/v1/appointments").WithTags("Scheduling");
        appointments.MapGet("/",(DateTimeOffset from,DateTimeOffset to,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>service.GetAppointmentsAsync(tenant.TenantId,from,to,ct)).RequireAuthorization(TenantPermissions.AppointmentsRead);
        appointments.MapPost("/",async(AppointmentInput input,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>Results.Created("/api/v1/appointments",await service.CreateAppointmentAsync(tenant.TenantId,input,ct))).RequireAuthorization(TenantPermissions.AppointmentsCreate);
        appointments.MapPut("/{id:guid}",async(Guid id,AppointmentInput input,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>await service.UpdateAppointmentAsync(tenant.TenantId,id,input,ct)is{}value?Results.Ok(value):Results.NotFound()).RequireAuthorization(TenantPermissions.AppointmentsUpdate);
        appointments.MapPatch("/{id:guid}/status",async(Guid id,AppointmentStatusInput input,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>await service.ChangeStatusAsync(tenant.TenantId,id,input.Status,ct)is{}value?Results.Ok(value):Results.NotFound()).RequireAuthorization(TenantPermissions.AppointmentsUpdate);
        appointments.MapPatch("/{id:guid}/cancel",async(Guid id,ITenantContext tenant,ISchedulingService service,CancellationToken ct)=>await service.ChangeStatusAsync(tenant.TenantId,id,Nexora.Domain.Scheduling.AppointmentStatus.Cancelled,ct)is{}value?Results.Ok(value):Results.NotFound()).RequireAuthorization(TenantPermissions.AppointmentsCancel);
        return endpoints;
    }
}
