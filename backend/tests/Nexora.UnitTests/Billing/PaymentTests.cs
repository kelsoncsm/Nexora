using Nexora.Domain.Billing;
namespace Nexora.UnitTests.Billing;
public sealed class PaymentTests
{
 [Fact]public void InvoicePreservesSnapshotAndApproves(){var now=DateTimeOffset.UtcNow;var start=now.AddDays(14);var end=start.AddMonths(1);var invoice=new BillingInvoice(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),BillingInterval.Monthly,99.90m,"BRL",start,end,now.AddDay(),now);invoice.Apply(BillingPaymentStatus.Approved,now.AddMinutes(1));Assert.Equal(99.90m,invoice.Amount);Assert.Equal("BRL",invoice.Currency);Assert.Equal(start,invoice.CoverageStart);Assert.Equal(end,invoice.CoverageEnd);Assert.Equal(InvoiceStatus.Paid,invoice.Status);Assert.NotNull(invoice.PaidAt);}
 [Fact]public void PaymentDoesNotRegressFromApprovedOrByOlderEvent(){var now=DateTimeOffset.UtcNow;var payment=new BillingPayment(Guid.NewGuid(),GatewayProvider.MercadoPago,"external",PaymentMethod.Pix,BillingPaymentStatus.Pending,10,"BRL",now,now);Assert.True(payment.Apply(BillingPaymentStatus.Approved,now.AddMinutes(2),now.AddMinutes(2)));Assert.False(payment.Apply(BillingPaymentStatus.Rejected,now.AddMinutes(1),now.AddMinutes(3)));Assert.Equal(BillingPaymentStatus.Approved,payment.Status);}
 [Fact]public void PlanPriceRequiresPositiveAmountAndIsoCurrency(){Assert.Throws<ArgumentException>(()=>new PlanPrice(Guid.NewGuid(),BillingInterval.Yearly,"BRL",0,DateTimeOffset.UtcNow));Assert.Throws<ArgumentException>(()=>new PlanPrice(Guid.NewGuid(),BillingInterval.Yearly,"REAL",10,DateTimeOffset.UtcNow));}
}
file static class DateExtensions{public static DateTimeOffset AddDay(this DateTimeOffset x)=>x.AddDays(1);}
