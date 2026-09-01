using System.Security.Claims;
using Nexora.Application.Administration;

namespace Nexora.Api.Administration;

internal static class TenantAdminEndpoints
{
    public static void MapTenantAdminEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/tenants",
            (IAdministrationService service, CancellationToken ct) => service.GetTenantsAsync(ct));
        admin.MapGet("/tenants/{id:guid}", GetTenantAsync);
        admin.MapPatch("/tenants/{id:guid}/status", SetTenantStatusAsync);
    }

    private static async Task<IResult> GetTenantAsync(Guid id, IAdministrationService service, CancellationToken ct) =>
        await service.GetTenantAsync(id, ct) is { } tenant ? Results.Ok(tenant) : Results.NotFound();

    private static async Task<IResult> SetTenantStatusAsync(
        Guid id,
        AdministrationEndpoints.StatusRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        IAdministrationService service,
        CancellationToken ct) =>
        await service.SetTenantActiveAsync(UserId(user), id, request.IsActive, context.TraceIdentifier, ct)
            ? Results.NoContent()
            : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
