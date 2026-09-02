using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Nexora.Infrastructure.Persistence;

internal static class PostgresAdvisoryLock
{
    public static bool IsSupported(NexoraDbContext db) => db.Database.IsNpgsql();

    /// <summary>
    /// Serializes a plan-limit "count-then-insert" for a tenant so two concurrent creates cannot both
    /// pass a near-limit count and overshoot (ADR-0019). Opens a transaction and takes a per-tenant,
    /// per-feature advisory lock held until commit/rollback. Returns <c>null</c> on providers without
    /// advisory locks (InMemory tests): the caller still runs the count, just without the guard.
    /// Commit after the insert; dispose without committing to roll back.
    /// </summary>
    public static async Task<IDbContextTransaction?> BeginPlanLimitGateAsync(
        NexoraDbContext db, string featureCode, Guid tenantId, CancellationToken cancellationToken)
    {
        if (!IsSupported(db)) return null;
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await AcquireAsync(db, $"plan-limit:{featureCode}", tenantId, null, cancellationToken);
        return transaction;
    }

    public static async Task AcquireAsync(NexoraDbContext db,string scope,Guid first,Guid? second,CancellationToken cancellationToken)
    {
        var bytes=Encoding.UTF8.GetBytes($"nexora:{scope}:{first:D}:{second?.ToString("D")??"-"}");
        var digest=SHA256.HashData(bytes);
        var key1=BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(0,4));
        var key2=BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(4,4));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key1}, {key2})",cancellationToken);
    }
}
