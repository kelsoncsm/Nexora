using Nexora.Application.Billing;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Billing;

internal static class TenantBillingEndpoints
{
    public static void MapTenantBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Billing and subscription are tenant-administrative surfaces (audit P2.1): viewing the
        // subscription/invoices and starting a checkout require tenant.manage, like the rest of the
        // tenant admin group. They are not feature-gated — an expired tenant with tenant.manage can
        // still see and settle its subscription (ADR-0019 keeps billing off the RequireFeature map).
        endpoints.MapGet("/api/v1/subscription", GetSubscriptionAsync)
            .RequireAuthorization(TenantPermissions.TenantManage)
            .WithTags("Billing");

        var tenantBilling = endpoints
            .MapGroup("/api/v1/billing")
            .RequireAuthorization(TenantPermissions.TenantManage)
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
