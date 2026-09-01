using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Billing;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Billing;

internal static class PaidCoverageLifecycle
{
    public static async Task<bool> TryApplyAsync(NexoraDbContext db,Subscription subscription,DateTimeOffset now,CancellationToken cancellationToken)
    {
        var expectedStart=subscription.Status==SubscriptionStatus.Trialing?subscription.TrialEndAt:subscription.CurrentPeriodEnd;
        var invoice=await db.BillingInvoices.Where(x=>x.SubscriptionId==subscription.Id&&x.Status==InvoiceStatus.Paid&&x.CoverageStart==expectedStart).OrderBy(x=>x.CoverageEnd).FirstOrDefaultAsync(cancellationToken);
        return invoice is not null&&subscription.ApplyPaidCoverage(invoice.CoverageStart,invoice.CoverageEnd,now);
    }
}
