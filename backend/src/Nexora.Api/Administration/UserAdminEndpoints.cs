using Nexora.Application.Administration;

namespace Nexora.Api.Administration;

internal static class UserAdminEndpoints
{
    public static void MapUserAdminEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/users",
            (IAdministrationService service, CancellationToken ct) => service.GetUsersAsync(ct));
    }
}
