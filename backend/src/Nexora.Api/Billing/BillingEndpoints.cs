using System.Security.Claims;
using Nexora.Application.Billing;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Billing;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/subscription",async(ITenantContext tenant,ISubscriptionService service,CancellationToken ct)=>await service.GetForTenantAsync(tenant.TenantId,ct)is{}value?Results.Ok(value):Results.NotFound()).RequireAuthorization(policy=>policy.RequireClaim("tenant_id")).WithTags("Billing");
        var tenantBilling=endpoints.MapGroup("/api/v1/billing").RequireAuthorization(policy=>policy.RequireClaim("tenant_id")).WithTags("Billing");
        tenantBilling.MapGet("/invoices",(ITenantContext tenant,IBillingPaymentService service,CancellationToken ct)=>service.GetTenantInvoicesAsync(tenant.TenantId,ct));
        tenantBilling.MapPost("/checkout",(CheckoutInput input,ITenantContext tenant,IBillingPaymentService service,CancellationToken ct)=>service.CreateCheckoutAsync(tenant.TenantId,input,ct)).RequireRateLimiting("checkout");
        endpoints.MapPost("/api/v1/payments/webhooks/mercado-pago",WebhookAsync).AllowAnonymous().RequireRateLimiting("webhook").WithTags("Payment Webhooks");
        var admin=endpoints.MapGroup("/api/v1/admin/subscriptions").RequireAuthorization("PlatformAdmin").WithTags("Billing Administration");
        admin.MapGet("/",(ISubscriptionService service,CancellationToken ct)=>service.GetAllAsync(ct));
        admin.MapGet("/{id:guid}",async(Guid id,ISubscriptionService service,CancellationToken ct)=>await service.GetAsync(id,ct)is{}value?Results.Ok(value):Results.NotFound());
        admin.MapGet("/{id:guid}/events",(Guid id,ISubscriptionService service,CancellationToken ct)=>service.GetEventsAsync(id,ct));
        admin.MapPost("/",async(CreateSubscriptionInput input,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Results.Created("/api/v1/admin/subscriptions",await service.CreateTrialAsync(UserId(user),input,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/activate",(Guid id,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.ActivateAsync(UserId(user),id,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/change-plan",(Guid id,ChangePlanInput input,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.ChangePlanAsync(UserId(user),id,input.PlanId,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/cancel-at-period-end",(Guid id,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.ScheduleCancellationAsync(UserId(user),id,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/cancel-immediately",(Guid id,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.CancelImmediatelyAsync(UserId(user),id,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/mark-past-due",(Guid id,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.MarkPastDueAsync(UserId(user),id,context.TraceIdentifier,ct)));
        admin.MapPost("/{id:guid}/process",(Guid id,ClaimsPrincipal user,HttpContext context,ISubscriptionService service,CancellationToken ct)=>Result(service.ProcessAsync(UserId(user),id,context.TraceIdentifier,ct)));
        var payments=endpoints.MapGroup("/api/v1/admin/billing").RequireAuthorization("PlatformAdmin").WithTags("Billing Administration");
        payments.MapGet("/prices",(IBillingPaymentService service,CancellationToken ct)=>service.GetPricesAsync(ct));payments.MapPost("/prices",async(CreatePlanPriceInput input,ClaimsPrincipal user,HttpContext context,IBillingPaymentService service,CancellationToken ct)=>Results.Created("/api/v1/admin/billing/prices",await service.SetPriceAsync(UserId(user),input,context.TraceIdentifier,ct)));payments.MapGet("/invoices",(IBillingPaymentService service,CancellationToken ct)=>service.GetInvoicesAsync(ct));payments.MapGet("/payments",(IBillingPaymentService service,CancellationToken ct)=>service.GetPaymentsAsync(ct));
        return endpoints;
    }
    private static async Task<IResult> WebhookAsync(HttpContext context,WebhookInput input,IWebhookSignatureValidator validator,IBillingPaymentService service,CancellationToken ct){var dataId=context.Request.Query["data.id"].FirstOrDefault()??input.Data?.Id;if(string.IsNullOrWhiteSpace(dataId)||!validator.Validate(context.Request.Headers["x-signature"].ToString(),context.Request.Headers["x-request-id"].ToString(),dataId))return Results.Unauthorized();var eventId=input.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture)??$"{input.Action}:{dataId}";await service.ProcessWebhookAsync(eventId,input.Action??input.Type??"payment",dataId,ct);return Results.Ok();}
    private static async Task<IResult> Result(Task<SubscriptionView?> operation)=>await operation is{}value?Results.Ok(value):Results.NotFound();
    private static Guid UserId(ClaimsPrincipal principal)=>Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)??principal.FindFirstValue("sub")??throw new InvalidOperationException("Subject claim is missing."));
    private sealed record ChangePlanInput(Guid PlanId);
    private sealed record WebhookData(string Id);private sealed record WebhookInput(long? Id,string? Type,string? Action,WebhookData? Data);
}
