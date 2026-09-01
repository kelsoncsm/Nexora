using System.Globalization;
using Nexora.Application.Billing;

namespace Nexora.Api.Billing;

internal static class PaymentWebhookEndpoints
{
    public static void MapPaymentWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/payments/webhooks/mercado-pago", WebhookAsync)
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .WithTags("Payment Webhooks");
    }

    private static async Task<IResult> WebhookAsync(
        HttpContext context,
        WebhookInput input,
        IWebhookSignatureValidator validator,
        IBillingPaymentService service,
        CancellationToken ct)
    {
        var dataId = context.Request.Query["data.id"].FirstOrDefault() ?? input.Data?.Id;
        if (string.IsNullOrWhiteSpace(dataId)
            || !validator.Validate(
                context.Request.Headers["x-signature"].ToString(),
                context.Request.Headers["x-request-id"].ToString(),
                dataId))
        {
            return Results.Unauthorized();
        }

        var eventId = input.Id?.ToString(CultureInfo.InvariantCulture) ?? $"{input.Action}:{dataId}";
        await service.ProcessWebhookAsync(eventId, input.Action ?? input.Type ?? "payment", dataId, ct);
        return Results.Ok();
    }

    private sealed record WebhookData(string Id);
    private sealed record WebhookInput(long? Id, string? Type, string? Action, WebhookData? Data);
}
