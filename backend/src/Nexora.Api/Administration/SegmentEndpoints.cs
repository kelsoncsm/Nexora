using System.Security.Claims;
using Nexora.Application.Administration;

namespace Nexora.Api.Administration;

internal static class SegmentEndpoints
{
    public static void MapSegmentEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/segments",
            (IAdministrationService service, CancellationToken ct) => service.GetSegmentsAsync(ct));
        admin.MapPost("/segments", CreateSegmentAsync);
        admin.MapPut("/segments/{id:guid}", UpdateSegmentAsync);
    }

    private static async Task<IResult> CreateSegmentAsync(
        AdministrationEndpoints.SegmentCreateRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        IAdministrationService service,
        CancellationToken ct)
    {
        var segment = await service.CreateSegmentAsync(
            UserId(user), request.Code, request.Name, context.TraceIdentifier, ct);
        return Results.Created("/api/v1/admin/segments", segment);
    }

    private static async Task<IResult> UpdateSegmentAsync(
        Guid id,
        AdministrationEndpoints.SegmentUpdateRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        IAdministrationService service,
        CancellationToken ct) =>
        await service.UpdateSegmentAsync(
            UserId(user), id, request.Name, request.IsActive, context.TraceIdentifier, ct) is { } segment
            ? Results.Ok(segment)
            : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
