namespace Nexora.Api.Billing;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapTenantBillingEndpoints();
        endpoints.MapPaymentWebhookEndpoints();
        endpoints.MapSubscriptionAdminEndpoints();
        endpoints.MapBillingAdminEndpoints();
        return endpoints;
    }
}
