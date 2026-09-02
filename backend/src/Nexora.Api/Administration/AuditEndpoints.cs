using Nexora.Application.Administration;

namespace Nexora.Api.Administration;

internal static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/audit-logs",
            (IAdministrationService service, CancellationToken ct) => service.GetAuditAsync(ct));
    }
}
