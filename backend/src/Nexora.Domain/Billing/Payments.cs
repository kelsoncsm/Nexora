namespace Nexora.Domain.Billing;

public enum InvoiceStatus { Pending, Paid, Failed, Canceled, Refunded }
public enum BillingPaymentStatus { Pending, Approved, Rejected, Canceled, Refunded }
public enum PaymentMethod { CreditCard, Pix, Boleto }
public enum GatewayProvider { MercadoPago }

public sealed class PlanPrice
{
    private PlanPrice() { }
    public PlanPrice(Guid planId,BillingInterval interval,string currency,decimal amount,DateTimeOffset now)
    { if(planId==Guid.Empty||amount<=0)throw new ArgumentException("A positive plan price is required.");currency=NormalizeCurrency(currency);Id=Guid.NewGuid();PlanId=planId;BillingInterval=interval;Currency=currency;Amount=amount;IsActive=true;CreatedAt=UpdatedAt=now.ToUniversalTime(); }
    public Guid Id{get;private set;}public Guid PlanId{get;private set;}public BillingInterval BillingInterval{get;private set;}public string Currency{get;private set;}=null!;public decimal Amount{get;private set;}public bool IsActive{get;private set;}public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
    public void Deactivate(DateTimeOffset now){IsActive=false;UpdatedAt=now.ToUniversalTime();}
    private static string NormalizeCurrency(string value){value=(value??string.Empty).Trim().ToUpperInvariant();if(value.Length!=3)throw new ArgumentException("Currency must be an ISO 4217 code.");return value;}
}

public sealed class BillingInvoice
{
    private BillingInvoice() { }
    public BillingInvoice(Guid tenantId,Guid subscriptionId,Guid planId,BillingInterval interval,decimal amount,string currency,DateTimeOffset coverageStart,DateTimeOffset coverageEnd,DateTimeOffset dueAt,DateTimeOffset now)
    { if(amount<=0)throw new ArgumentException("Invoice amount must be positive.");if(coverageEnd<=coverageStart)throw new ArgumentException("Invoice coverage period is invalid.");Id=Guid.NewGuid();TenantId=tenantId;SubscriptionId=subscriptionId;PlanId=planId;BillingInterval=interval;Amount=amount;Currency=currency;CoverageStart=coverageStart.ToUniversalTime();CoverageEnd=coverageEnd.ToUniversalTime();Status=InvoiceStatus.Pending;DueAt=dueAt.ToUniversalTime();CreatedAt=UpdatedAt=now.ToUniversalTime(); }
    public Guid Id{get;private set;}public Guid TenantId{get;private set;}public Guid SubscriptionId{get;private set;}public Guid PlanId{get;private set;}public BillingInterval BillingInterval{get;private set;}public decimal Amount{get;private set;}public string Currency{get;private set;}=null!;public DateTimeOffset CoverageStart{get;private set;}public DateTimeOffset CoverageEnd{get;private set;}public InvoiceStatus Status{get;private set;}public DateTimeOffset DueAt{get;private set;}public DateTimeOffset? PaidAt{get;private set;}public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
    public void Apply(BillingPaymentStatus status,DateTimeOffset now){if(Status is InvoiceStatus.Refunded or InvoiceStatus.Canceled)return;if(status==BillingPaymentStatus.Approved){Status=InvoiceStatus.Paid;PaidAt??=now.ToUniversalTime();}else if(status==BillingPaymentStatus.Rejected&&Status!=InvoiceStatus.Paid)Status=InvoiceStatus.Failed;else if(status==BillingPaymentStatus.Refunded&&Status==InvoiceStatus.Paid)Status=InvoiceStatus.Refunded;UpdatedAt=now.ToUniversalTime();}
}

public sealed class BillingPayment
{
    private BillingPayment() { }
    public BillingPayment(Guid invoiceId,GatewayProvider gateway,string externalId,PaymentMethod method,BillingPaymentStatus status,decimal amount,string currency,DateTimeOffset gatewayUpdatedAt,DateTimeOffset now)
    { Id=Guid.NewGuid();BillingInvoiceId=invoiceId;Gateway=gateway;ExternalPaymentId=externalId;PaymentMethod=method;Status=status;Amount=amount;Currency=currency;GatewayUpdatedAt=gatewayUpdatedAt.ToUniversalTime();CreatedAt=UpdatedAt=now.ToUniversalTime(); }
    public Guid Id{get;private set;}public Guid BillingInvoiceId{get;private set;}public GatewayProvider Gateway{get;private set;}public string ExternalPaymentId{get;private set;}=null!;public PaymentMethod PaymentMethod{get;private set;}public BillingPaymentStatus Status{get;private set;}public decimal Amount{get;private set;}public string Currency{get;private set;}=null!;public DateTimeOffset GatewayUpdatedAt{get;private set;}public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
    public bool Apply(BillingPaymentStatus status,DateTimeOffset gatewayUpdatedAt,DateTimeOffset now){gatewayUpdatedAt=gatewayUpdatedAt.ToUniversalTime();if(gatewayUpdatedAt<GatewayUpdatedAt)return false;if(Rank(status)<Rank(Status))return false;Status=status;GatewayUpdatedAt=gatewayUpdatedAt;UpdatedAt=now.ToUniversalTime();return true;}
    private static int Rank(BillingPaymentStatus x)=>x switch{BillingPaymentStatus.Pending=>0,BillingPaymentStatus.Rejected=>1,BillingPaymentStatus.Approved=>2,BillingPaymentStatus.Canceled=>3,BillingPaymentStatus.Refunded=>4,_=>0};
}

public sealed class ProcessedWebhookEvent
{
    private ProcessedWebhookEvent() { }
    public ProcessedWebhookEvent(GatewayProvider provider,string externalEventId,string eventType,DateTimeOffset processedAt){Id=Guid.NewGuid();Provider=provider;ExternalEventId=externalEventId;EventType=eventType;ProcessedAt=processedAt.ToUniversalTime();}
    public Guid Id{get;private set;}public GatewayProvider Provider{get;private set;}public string ExternalEventId{get;private set;}=null!;public string EventType{get;private set;}=null!;public DateTimeOffset ProcessedAt{get;private set;}
}
