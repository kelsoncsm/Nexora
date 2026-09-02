using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.Application.Administration;
using Nexora.Domain.Administration;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Administration;

/// <summary>
/// Adds the audit row to the shared <see cref="NexoraDbContext"/> (ADR-0021). It is written by the
/// caller's <c>SaveChangesAsync</c>/transaction, so it commits or rolls back together with the
/// business mutation. Details are serialised camelCase with enums as their names.
/// </summary>
public sealed class AuditLogWriter(NexoraDbContext db, TimeProvider clock) : IAuditLogWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Record(Guid actorUserId, string action, string targetType, string targetId,
        string correlationId, Guid? tenantId = null, object? details = null) =>
        db.AuditLogs.Add(new AuditLog(
            actorUserId, action, targetType, targetId, succeeded: true,
            correlationId ?? string.Empty, clock.GetUtcNow(), tenantId,
            details is null ? null : JsonSerializer.Serialize(details, Json)));
}
