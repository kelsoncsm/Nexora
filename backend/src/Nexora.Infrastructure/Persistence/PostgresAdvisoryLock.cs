using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Persistence;

internal static class PostgresAdvisoryLock
{
    public static bool IsSupported(NexoraDbContext db) => db.Database.IsNpgsql();

    public static async Task AcquireAsync(NexoraDbContext db,string scope,Guid first,Guid? second,CancellationToken cancellationToken)
    {
        var bytes=Encoding.UTF8.GetBytes($"nexora:{scope}:{first:D}:{second?.ToString("D")??"-"}");
        var digest=SHA256.HashData(bytes);
        var key1=BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(0,4));
        var key2=BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(4,4));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key1}, {key2})",cancellationToken);
    }
}
