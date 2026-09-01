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
        var productivity = await appointments.Where(x => x.Status == AppointmentStatus.Completed)
            .GroupBy(x => new { x.ProfessionalId })
            .Select(group => new { group.Key.ProfessionalId, Count = group.Count() })
            .Join(dbContext.Professionals.Where(x => x.TenantId == tenantId), x => x.ProfessionalId, x => x.Id,
                (metric, professional) => new ProfessionalProductivity(professional.Id, professional.Name, metric.Count))
            .OrderByDescending(x => x.CompletedAppointments).ToListAsync(cancellationToken);
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
