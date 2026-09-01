using System.Security.Claims;
using Nexora.Application.Administration;
using Nexora.Application.Plans;

namespace Nexora.Api.Administration;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin").RequireAuthorization("PlatformAdmin").WithTags("Platform Administration");
        admin.MapGet("/dashboard", (IAdministrationService service, CancellationToken ct) => service.DashboardAsync(ct));
        admin.MapGet("/tenants", (IAdministrationService service, CancellationToken ct) => service.GetTenantsAsync(ct));
        admin.MapGet("/tenants/{id:guid}", async (Guid id, IAdministrationService service, CancellationToken ct) =>
            await service.GetTenantAsync(id, ct) is { } tenant ? Results.Ok(tenant) : Results.NotFound());
        admin.MapPatch("/tenants/{id:guid}/status", async (Guid id, StatusRequest request, ClaimsPrincipal user,
            HttpContext context, IAdministrationService service, CancellationToken ct) =>
            await service.SetTenantActiveAsync(UserId(user), id, request.IsActive, context.TraceIdentifier, ct) ? Results.NoContent() : Results.NotFound());
        admin.MapGet("/users", (IAdministrationService service, CancellationToken ct) => service.GetUsersAsync(ct));
        admin.MapGet("/segments", (IAdministrationService service, CancellationToken ct) => service.GetSegmentsAsync(ct));
        admin.MapPost("/segments", async (SegmentCreateRequest request, ClaimsPrincipal user, HttpContext context,
            IAdministrationService service, CancellationToken ct) => Results.Created("/api/v1/admin/segments",
                await service.CreateSegmentAsync(UserId(user), request.Code, request.Name, context.TraceIdentifier, ct)));
        admin.MapPut("/segments/{id:guid}", async (Guid id, SegmentUpdateRequest request, ClaimsPrincipal user,
            HttpContext context, IAdministrationService service, CancellationToken ct) =>
            await service.UpdateSegmentAsync(UserId(user), id, request.Name, request.IsActive, context.TraceIdentifier, ct) is { } segment
                ? Results.Ok(segment) : Results.NotFound());
        admin.MapGet("/audit-logs", (IAdministrationService service, CancellationToken ct) => service.GetAuditAsync(ct));
        admin.MapGet("/features", (IPlanCatalogService s,CancellationToken ct)=>s.GetFeaturesAsync(ct));
        admin.MapPost("/features", async (CatalogCreate r,IPlanCatalogService s,CancellationToken ct)=>Results.Created("/api/v1/admin/features",await s.CreateFeatureAsync(r.Code,r.Name,ct)));
        admin.MapPut("/features/{id:guid}", async(Guid id,CatalogUpdate r,IPlanCatalogService s,CancellationToken ct)=>await s.UpdateFeatureAsync(id,r.Name,r.IsActive,ct) is { } x?Results.Ok(x):Results.NotFound());
        admin.MapGet("/plans", (IPlanCatalogService s,CancellationToken ct)=>s.GetPlansAsync(ct));
        admin.MapPost("/plans", async(CatalogCreate r,IPlanCatalogService s,CancellationToken ct)=>Results.Created("/api/v1/admin/plans",await s.CreatePlanAsync(r.Code,r.Name,ct)));
        admin.MapPut("/plans/{id:guid}", async(Guid id,PlanUpdate r,IPlanCatalogService s,CancellationToken ct)=>await s.UpdatePlanAsync(id,r.Name,r.IsActive,r.IsPublic,r.IsTrialEligible,ct) is { } x?Results.Ok(x):Results.NotFound());
        admin.MapPut("/plans/{planId:guid}/features/{featureId:guid}", async(Guid planId,Guid featureId,AccessConfiguration r,IPlanCatalogService s,CancellationToken ct)=>await s.ConfigurePlanFeatureAsync(planId,featureId,r.Enabled,r.Limit,ct) is { } x?Results.Ok(x):Results.NotFound());
        admin.MapGet("/tenants/{tenantId:guid}/feature-overrides", (Guid tenantId,IPlanCatalogService s,CancellationToken ct)=>s.GetOverridesAsync(tenantId,ct));
        admin.MapPut("/tenants/{tenantId:guid}/feature-overrides/{featureId:guid}", async(Guid tenantId,Guid featureId,AccessConfiguration r,IPlanCatalogService s,CancellationToken ct)=>await s.ConfigureOverrideAsync(tenantId,featureId,r.Enabled,r.Limit,ct) is { } x?Results.Ok(x):Results.NotFound());
        return endpoints;
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Subject claim is missing."));
    private sealed record StatusRequest(bool IsActive);
    private sealed record SegmentCreateRequest(string Code, string Name);
    private sealed record SegmentUpdateRequest(string Name, bool IsActive);
    private sealed record CatalogCreate(string Code,string Name);
    private sealed record CatalogUpdate(string Name,bool IsActive);
    private sealed record PlanUpdate(string Name,bool IsActive,bool IsPublic,bool IsTrialEligible);
    private sealed record AccessConfiguration(bool Enabled,int? Limit);
}
