using Nexora.Application.Billing;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Billing;

internal static class TenantBillingEndpoints
{
    public static void MapTenantBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/subscription", GetSubscriptionAsync)
            .RequireAuthorization(policy => policy.RequireClaim("tenant_id"))
            .WithTags("Billing");

        var tenantBilling = endpoints
            .MapGroup("/api/v1/billing")
            .RequireAuthorization(policy => policy.RequireClaim("tenant_id"))
            .WithTags("Billing");

        tenantBilling.MapGet(
            "/invoices",
            (ITenantContext tenant, IBillingPaymentService service, CancellationToken ct) =>
                service.GetTenantInvoicesAsync(tenant.TenantId, ct));

        tenantBilling.MapPost(
                "/checkout",
                (CheckoutInput input, ITenantContext tenant, IBillingPaymentService service, CancellationToken ct) =>
                    service.CreateCheckoutAsync(tenant.TenantId, input, ct))
            .RequireRateLimiting("checkout");
    }

    private static async Task<IResult> GetSubscriptionAsync(
        ITenantContext tenant, ISubscriptionService service, CancellationToken ct) =>
        await service.GetForTenantAsync(tenant.TenantId, ct) is { } value
            ? Results.Ok(value)
            : Results.NotFound();
}
