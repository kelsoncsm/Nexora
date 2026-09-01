using Nexora.Application.Administration;

namespace Nexora.Api.Administration;

internal static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/dashboard",
            (IAdministrationService service, CancellationToken ct) => service.DashboardAsync(ct));
    }
}
