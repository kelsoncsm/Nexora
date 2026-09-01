using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexora.Domain.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Notifications;

public sealed class EmailOutboxHealthCheck(NexoraDbContext db, TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var failed = await db.EmailOutboxMessages.CountAsync(x => x.Status == EmailOutboxStatus.Failed, cancellationToken);
        var oldest = await db.EmailOutboxMessages.Where(x => x.Status == EmailOutboxStatus.Pending)
            .OrderBy(x => x.CreatedAt).Select(x => (DateTimeOffset?)x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var data = new Dictionary<string, object> { ["failed"] = failed, ["oldestPendingMinutes"] = oldest.HasValue ? Math.Max(0, (now - oldest.Value).TotalMinutes) : 0 };
        return failed > 0 || oldest < now.AddMinutes(-5)
            ? HealthCheckResult.Degraded("Email outbox requires operational attention.", data: data)
            : HealthCheckResult.Healthy("Email outbox is within the operational target.", data);
    }
}
