using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Nexora.Application.Billing;
using Nexora.Domain.Administration;
using Nexora.Domain.Billing;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Billing;

public sealed class BillingPaymentService(NexoraDbContext db,IPaymentGateway gateway,TimeProvider clock):IBillingPaymentService
{
    private const string Currency="BRL";
    public async Task<PlanPriceView> SetPriceAsync(Guid actor,CreatePlanPriceInput input,string correlationId,CancellationToken ct)
    {
        if(!string.Equals(input.Currency.Trim(),Currency,StringComparison.OrdinalIgnoreCase))throw new PaymentValidationException("Only BRL is supported in the MVP.");
        if(!await db.Plans.AnyAsync(x=>x.Id==input.PlanId&&x.IsActive,ct))throw new PaymentValidationException("Plan is unavailable.");
        var now=clock.GetUtcNow();var current=await db.PlanPrices.Where(x=>x.PlanId==input.PlanId&&x.BillingInterval==input.BillingInterval&&x.Currency==Currency&&x.IsActive).ToArrayAsync(ct);
        foreach(var x in current)x.Deactivate(now);
        PlanPrice price;try{price=new PlanPrice(input.PlanId,input.BillingInterval,Currency,input.Amount,now);}catch(ArgumentException ex){throw new PaymentValidationException(ex.Message);}
        db.Add(price);db.AuditLogs.Add(new AuditLog(actor,"plan_price.changed","Plan",input.PlanId.ToString(),true,correlationId,now));await Save(ct);return Price(price);
    }
    public async Task<IReadOnlyList<PlanPriceView>> GetPricesAsync(CancellationToken ct)=>await db.PlanPrices.OrderByDescending(x=>x.CreatedAt).Select(x=>new PlanPriceView(x.Id,x.PlanId,x.BillingInterval,x.Currency,x.Amount,x.IsActive,x.CreatedAt)).ToArrayAsync(ct);

    public async Task<CheckoutView> CreateCheckoutAsync(Guid tenantId,CheckoutInput input,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(input.PayerEmail))throw new PaymentValidationException("Payer email is required.");
        var requestStartedAt=clock.GetUtcNow();await using var transaction=await BeginTransactionAsync(ct);
        var subscription=await db.Subscriptions.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&(x.Status==SubscriptionStatus.Trialing||x.Status==SubscriptionStatus.Active||x.Status==SubscriptionStatus.PastDue),ct)??throw new PaymentValidationException("A current subscription is required.");
        if(transaction is not null){await PostgresAdvisoryLock.AcquireAsync(db,"billing-checkout",subscription.Id,null,ct);await db.Entry(subscription).ReloadAsync(ct);}
        var price=await db.PlanPrices.SingleOrDefaultAsync(x=>x.PlanId==subscription.PlanId&&x.BillingInterval==subscription.BillingInterval&&x.Currency==Currency&&x.IsActive,ct)??throw new PaymentValidationException("An active BRL price was not found for this plan and interval.");
        var period=BillingPeriodPolicy.Next(subscription);
        var invoice=await db.BillingInvoices.SingleOrDefaultAsync(x=>x.SubscriptionId==subscription.Id&&x.CoverageStart==period.Start&&x.CoverageEnd==period.End,ct);
        if(invoice is null){invoice=new BillingInvoice(tenantId,subscription.Id,subscription.PlanId,subscription.BillingInterval,price.Amount,price.Currency,period.Start,period.End,requestStartedAt.AddDays(1),requestStartedAt);db.Add(invoice);await Save(ct);}
        else if(invoice.Status==InvoiceStatus.Canceled)throw new PaymentConflictException("The billing obligation was canceled and cannot be charged again.");
        var latest=await db.BillingPayments.Where(x=>x.BillingInvoiceId==invoice.Id).OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync(ct);
        if(latest is not null&&(invoice.Status==InvoiceStatus.Paid||latest.Status==BillingPaymentStatus.Pending||latest.CreatedAt>=requestStartedAt)){await CommitAsync(transaction,ct);return Checkout(invoice,latest,null,null);}
        GatewayCheckoutResult result;
        try{result=await gateway.CreateCheckoutAsync(new GatewayCheckoutRequest(invoice.Id,invoice.Amount,invoice.Currency,input.PaymentMethod,input.PayerEmail,input.PaymentToken,input.CardPaymentMethodId,InvoiceIdempotencyKey(invoice)),ct);}
        catch{await CommitAsync(transaction,ct);db.ChangeTracker.Clear();throw;}
        var existing=await db.BillingPayments.SingleOrDefaultAsync(x=>x.Gateway==gateway.Provider&&x.ExternalPaymentId==result.ExternalPaymentId,ct);
        if(existing is not null){await CommitAsync(transaction,ct);return Checkout(invoice,existing,result.CheckoutUrl,result.QrCode);}
        var payment=new BillingPayment(invoice.Id,gateway.Provider,result.ExternalPaymentId,input.PaymentMethod,result.Status,invoice.Amount,invoice.Currency,result.UpdatedAt,requestStartedAt);
        db.Add(payment);ApplyFinancialState(subscription,invoice,payment,result.Status,requestStartedAt);await Save(ct);await CommitAsync(transaction,ct);return Checkout(invoice,payment,result.CheckoutUrl,result.QrCode);
    }

    public async Task<bool> ProcessWebhookAsync(string eventId,string eventType,string externalPaymentId,CancellationToken ct)
    {
        if(await db.ProcessedWebhookEvents.AnyAsync(x=>x.Provider==gateway.Provider&&x.ExternalEventId==eventId,ct))return false;
        var payment=await db.BillingPayments.SingleOrDefaultAsync(x=>x.Gateway==gateway.Provider&&x.ExternalPaymentId==externalPaymentId,ct);if(payment is null)throw new PaymentValidationException("Payment is unknown.");
        var reconciled=await gateway.GetPaymentAsync(externalPaymentId,ct);var invoice=await db.BillingInvoices.SingleAsync(x=>x.Id==payment.BillingInvoiceId,ct);
        if(reconciled.Amount!=invoice.Amount||!string.Equals(reconciled.Currency,invoice.Currency,StringComparison.OrdinalIgnoreCase))throw new PaymentConflictException("Gateway payment does not match the invoice snapshot.");
        var now=clock.GetUtcNow();var changed=payment.Apply(reconciled.Status,reconciled.UpdatedAt,now);if(changed){var subscription=await db.Subscriptions.SingleAsync(x=>x.Id==invoice.SubscriptionId,ct);ApplyFinancialState(subscription,invoice,payment,reconciled.Status,now);}
        db.Add(new ProcessedWebhookEvent(gateway.Provider,eventId,eventType,now));await Save(ct);return changed;
    }
    private void ApplyFinancialState(Subscription subscription,BillingInvoice invoice,BillingPayment payment,BillingPaymentStatus status,DateTimeOffset now)
    {
        invoice.Apply(status,now);try{if(status==BillingPaymentStatus.Approved)subscription.ApplyPaidCoverage(invoice.CoverageStart,invoice.CoverageEnd,now);else if(status==BillingPaymentStatus.Rejected&&subscription.Status==SubscriptionStatus.Active)subscription.MarkPastDue(now);}catch(InvalidOperationException ex){throw new PaymentConflictException(ex.Message);}
        var type=status==BillingPaymentStatus.Approved?"billing.payment_approved":status==BillingPaymentStatus.Rejected?"billing.payment_rejected":"billing.payment_reconciled";var ev=new SubscriptionEvent(subscription.Id,null,type,$"invoiceId={invoice.Id};paymentId={payment.Id}",now);subscription.Events.Add(ev);db.SubscriptionEvents.Add(ev);
    }
    public async Task<IReadOnlyList<InvoiceView>> GetTenantInvoicesAsync(Guid tenantId,CancellationToken ct)=>await ProjectInvoices(db.BillingInvoices.Where(x=>x.TenantId==tenantId)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<InvoiceView>> GetInvoicesAsync(CancellationToken ct)=>await ProjectInvoices(db.BillingInvoices).ToArrayAsync(ct);
    public async Task<IReadOnlyList<PaymentView>> GetPaymentsAsync(CancellationToken ct)=>await db.BillingPayments.OrderByDescending(x=>x.CreatedAt).Select(x=>new PaymentView(x.Id,x.BillingInvoiceId,x.ExternalPaymentId,x.Gateway,x.PaymentMethod,x.Status,x.Amount,x.Currency,x.UpdatedAt)).ToArrayAsync(ct);
    private static IQueryable<InvoiceView> ProjectInvoices(IQueryable<BillingInvoice> q)=>q.OrderByDescending(x=>x.CreatedAt).Select(x=>new InvoiceView(x.Id,x.TenantId,x.SubscriptionId,x.PlanId,x.BillingInterval,x.Amount,x.Currency,x.CoverageStart,x.CoverageEnd,x.Status,x.DueAt,x.PaidAt,x.CreatedAt));
    private static PlanPriceView Price(PlanPrice x)=>new(x.Id,x.PlanId,x.BillingInterval,x.Currency,x.Amount,x.IsActive,x.CreatedAt);
    private static CheckoutView Checkout(BillingInvoice i,BillingPayment p,string? url,string? qr)=>new(i.Id,p.Id,i.Amount,i.Currency,p.ExternalPaymentId,p.Status,url,qr);
    private static string InvoiceIdempotencyKey(BillingInvoice invoice)=>$"nexora-invoice-{invoice.Id:D}";
    private async Task Save(CancellationToken ct){try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){throw new PaymentConflictException("The billing operation conflicts with an existing financial record.");}}
    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct)=>PostgresAdvisoryLock.IsSupported(db)?await db.Database.BeginTransactionAsync(ct):null;
    private static Task CommitAsync(IDbContextTransaction? transaction,CancellationToken ct)=>transaction is null?Task.CompletedTask:transaction.CommitAsync(ct);
}
