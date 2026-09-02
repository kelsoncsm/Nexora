using Nexora.Api.Plans;
using Nexora.Application.Plans;
using Nexora.Application.Scheduling;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Scheduling;

internal static class WorkingHoursEndpoints
{
    public static void MapWorkingHoursEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var working = endpoints.MapGroup("/api/v1/working-hours").WithTags("Scheduling").RequireFeature(FeatureCodes.Scheduling);

        working.MapGet(
                "/",
                (Guid? professionalId, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
                    service.GetWorkingHoursAsync(tenant.TenantId, professionalId, ct))
            .RequireAuthorization(TenantPermissions.AppointmentsRead);

        working.MapPut(
                "/",
                (WorkingHoursInput input, ITenantContext tenant, ISchedulingService service, CancellationToken ct) =>
                    service.SetWorkingHoursAsync(tenant.TenantId, input, ct))
            .RequireAuthorization(TenantPermissions.AppointmentsUpdate);
    }
}
