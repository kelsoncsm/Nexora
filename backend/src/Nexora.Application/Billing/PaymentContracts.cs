using Nexora.Domain.Billing;
namespace Nexora.Application.Billing;

public sealed record GatewayCheckoutRequest(Guid InvoiceId,decimal Amount,string Currency,PaymentMethod PaymentMethod,string PayerEmail,string? PaymentToken,string? CardPaymentMethodId,string IdempotencyKey);
public sealed record GatewayCheckoutResult(string ExternalPaymentId,BillingPaymentStatus Status,string? CheckoutUrl,string? QrCode,DateTimeOffset UpdatedAt);
public sealed record GatewayPaymentResult(string ExternalPaymentId,BillingPaymentStatus Status,decimal Amount,string Currency,PaymentMethod PaymentMethod,DateTimeOffset UpdatedAt);
public interface IPaymentGateway
{
    GatewayProvider Provider{get;} Task<GatewayCheckoutResult> CreateCheckoutAsync(GatewayCheckoutRequest request,CancellationToken ct);Task<GatewayPaymentResult> GetPaymentAsync(string externalPaymentId,CancellationToken ct);
}
public interface IWebhookSignatureValidator{bool Validate(string signature,string requestId,string dataId);}
public sealed record CreatePlanPriceInput(Guid PlanId,BillingInterval BillingInterval,string Currency,decimal Amount);
public sealed record PlanPriceView(Guid Id,Guid PlanId,BillingInterval BillingInterval,string Currency,decimal Amount,bool IsActive,DateTimeOffset CreatedAt);
public sealed record CheckoutInput(PaymentMethod PaymentMethod,string PayerEmail,string? PaymentToken,string? CardPaymentMethodId);
public sealed record CheckoutView(Guid InvoiceId,Guid PaymentId,decimal Amount,string Currency,string ExternalPaymentId,BillingPaymentStatus Status,string? CheckoutUrl,string? QrCode);
public sealed record InvoiceView(Guid Id,Guid TenantId,Guid SubscriptionId,Guid PlanId,BillingInterval BillingInterval,decimal Amount,string Currency,DateTimeOffset CoverageStart,DateTimeOffset CoverageEnd,InvoiceStatus Status,DateTimeOffset DueAt,DateTimeOffset? PaidAt,DateTimeOffset CreatedAt);
public sealed record PaymentView(Guid Id,Guid InvoiceId,string ExternalPaymentId,GatewayProvider Gateway,PaymentMethod PaymentMethod,BillingPaymentStatus Status,decimal Amount,string Currency,DateTimeOffset UpdatedAt);
public interface IBillingPaymentService
{
    Task<PlanPriceView> SetPriceAsync(Guid actor,CreatePlanPriceInput input,string correlationId,CancellationToken ct);Task<IReadOnlyList<PlanPriceView>> GetPricesAsync(CancellationToken ct);Task<CheckoutView> CreateCheckoutAsync(Guid tenantId,CheckoutInput input,CancellationToken ct);Task<IReadOnlyList<InvoiceView>> GetTenantInvoicesAsync(Guid tenantId,CancellationToken ct);Task<IReadOnlyList<InvoiceView>> GetInvoicesAsync(CancellationToken ct);Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(CancellationToken ct);Task<bool> ProcessWebhookAsync(string externalEventId,string eventType,string externalPaymentId,CancellationToken ct);
}
public sealed class PaymentValidationException(string message):Exception(message);public sealed class PaymentConflictException(string message):Exception(message);public sealed class PaymentGatewayUnavailableException(string message):Exception(message);
