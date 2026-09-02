using Microsoft.EntityFrameworkCore;
using Nexora.Application.Reports;
using Nexora.Application.Tenancy;
using Nexora.Domain.Billing;
using Nexora.Domain.Scheduling;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Reports;

public sealed class ReportService(NexoraDbContext dbContext, ITenantContext tenantContext) : IReportService
{
    public async Task<TenantReport> GetTenantReportAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var period = Validate(from, until); var tenantId = tenantContext.TenantId;
        var appointments = dbContext.Appointments.Where(x => x.TenantId == tenantId && x.StartAt >= period.From && x.StartAt < period.To);
        var appointmentMetrics = new AppointmentMetrics(
            await appointments.CountAsync(cancellationToken),
            await appointments.CountAsync(x => x.Status == AppointmentStatus.Completed, cancellationToken),
            await appointments.CountAsync(x => x.Status == AppointmentStatus.Cancelled, cancellationToken),
            await appointments.CountAsync(x => x.Status == AppointmentStatus.NoShow, cancellationToken));
        // Aggregation, join and ordering stay in SQL over scalar/anonymous shapes; only the
        // final record projection runs in memory over the already-aggregated, bounded result
        // (at most one row per tenant professional). Ordering the projected positional record
        // directly is not translatable by the Npgsql provider.
        var productivity = (await appointments.Where(x => x.Status == AppointmentStatus.Completed)
            .GroupBy(x => x.ProfessionalId)
            .Select(group => new { ProfessionalId = group.Key, Count = group.Count() })
            .Join(dbContext.Professionals.Where(x => x.TenantId == tenantId), metric => metric.ProfessionalId, professional => professional.Id,
                (metric, professional) => new { professional.Id, professional.Name, metric.Count })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken))
            .Select(x => new ProfessionalProductivity(x.Id, x.Name, x.Count))
            .ToList();
        var customers = await dbContext.Customers.CountAsync(x => x.TenantId == tenantId && x.CreatedAt < period.To, cancellationToken);
        return new(period, customers, appointmentMetrics, productivity);
    }

    public async Task<PlatformReport> GetPlatformReportAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var period = Validate(from, until);
        return new(period,
            await dbContext.Tenants.CountAsync(x => x.CreatedAt < period.To, cancellationToken),
            await dbContext.Tenants.CountAsync(x => x.IsActive && x.CreatedAt < period.To, cancellationToken),
            await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Trialing, cancellationToken),
            await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.Active, cancellationToken),
            await dbContext.Subscriptions.CountAsync(x => x.Status == SubscriptionStatus.PastDue, cancellationToken),
            await dbContext.BillingInvoices.Where(x => x.Status == InvoiceStatus.Paid && x.PaidAt >= period.From && x.PaidAt < period.To).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0,
            "BRL");
    }

    private static ReportPeriod Validate(DateTimeOffset from, DateTimeOffset to)
    {
        from = from.ToUniversalTime(); to = to.ToUniversalTime();
        if (to <= from || to - from > TimeSpan.FromDays(366)) throw new ReportValidationException("Report period must be positive and no longer than 366 days.");
        return new(from, to);
    }
}
