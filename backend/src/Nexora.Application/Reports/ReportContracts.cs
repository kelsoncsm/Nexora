namespace Nexora.Application.Reports;

public sealed record ReportPeriod(DateTimeOffset From, DateTimeOffset To);
public sealed record AppointmentMetrics(int Total, int Completed, int Cancelled, int NoShow);
public sealed record ProfessionalProductivity(Guid ProfessionalId, string ProfessionalName, int CompletedAppointments);
public sealed record TenantReport(
    ReportPeriod Period,
    int Customers,
    AppointmentMetrics Appointments,
    IReadOnlyList<ProfessionalProductivity> Productivity);
public sealed record PlatformReport(
    ReportPeriod Period,
    int TotalTenants,
    int ActiveTenants,
    int TrialSubscriptions,
    int ActiveSubscriptions,
    int PastDueSubscriptions,
    decimal PaidRevenue,
    string Currency);

public interface IReportService
{
    Task<TenantReport> GetTenantReportAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken);
    Task<PlatformReport> GetPlatformReportAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken);
}

public sealed class ReportValidationException(string message) : Exception(message);
