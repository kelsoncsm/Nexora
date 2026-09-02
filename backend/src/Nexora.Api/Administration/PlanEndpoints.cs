using System.Security.Claims;
using Nexora.Application.Plans;

namespace Nexora.Api.Administration;

internal static class PlanEndpoints
{
    public static void MapPlanEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/plans",
            (IPlanCatalogService service, CancellationToken ct) => service.GetPlansAsync(ct));
        admin.MapPost("/plans", CreatePlanAsync);
        admin.MapPut("/plans/{id:guid}", UpdatePlanAsync);
        admin.MapPut("/plans/{planId:guid}/features/{featureId:guid}", ConfigurePlanFeatureAsync);
    }

    private static async Task<IResult> CreatePlanAsync(
        AdministrationEndpoints.CatalogCreate request,
        IPlanCatalogService service,
        CancellationToken ct)
    {
        var plan = await service.CreatePlanAsync(request.Code, request.Name, ct);
        return Results.Created("/api/v1/admin/plans", plan);
    }

    private static async Task<IResult> UpdatePlanAsync(
        Guid id,
        AdministrationEndpoints.PlanUpdate request,
        IPlanCatalogService service,
        CancellationToken ct) =>
        await service.UpdatePlanAsync(
            id, request.Name, request.IsActive, request.IsPublic, request.IsTrialEligible, ct) is { } plan
            ? Results.Ok(plan)
            : Results.NotFound();

    private static async Task<IResult> ConfigurePlanFeatureAsync(
        Guid planId,
        Guid featureId,
        AdministrationEndpoints.AccessConfiguration request,
        ClaimsPrincipal user,
        HttpContext http,
        IPlanCatalogService service,
        CancellationToken ct) =>
        await service.ConfigurePlanFeatureAsync(UserId(user), planId, featureId, request.Enabled, request.Limit, http.TraceIdentifier, ct) is { } plan
            ? Results.Ok(plan)
            : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
