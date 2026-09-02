using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Nexora shares the <c>saas_dev</c> PostgreSQL database with other systems (notably the
/// <c>dentalflow</c> schema). This guard makes it impossible for a misconfiguration to point the
/// application's schema-provisioning at anything other than the two schemas Nexora owns
/// (<see cref="DatabaseSchemas.Application"/> at runtime, <see cref="DatabaseSchemas.IntegrationTests"/>
/// for the integration suite). There is deliberately no generic "create whatever schema is
/// configured" path, and nothing here ever drops a schema.
/// </summary>
public static class NexoraSchemaGuard
{
    /// <summary>The only schemas Nexora is ever allowed to create or reset.</summary>
    public static readonly IReadOnlySet<string> Owned =
        new HashSet<string>(StringComparer.Ordinal)
        {
            DatabaseSchemas.Application,
            DatabaseSchemas.IntegrationTests,
        };

    /// <summary>Returns <paramref name="schema"/> if Nexora owns it; throws otherwise.</summary>
    public static string EnsureOwned(string schema)
        => Owned.Contains(schema) ? schema : throw Rejected(schema);

    /// <summary>
    /// Creates the schema if it is missing, after validating Nexora owns it. Idempotent; never drops.
    /// </summary>
    public static async Task CreateIfMissingAsync(
        DatabaseFacade database, string schema, CancellationToken cancellationToken = default)
    {
        // Map to a fixed literal per owned schema — no identifier is ever interpolated into DDL.
        var sql = schema switch
        {
            DatabaseSchemas.Application => "CREATE SCHEMA IF NOT EXISTS \"nexora\"",
            DatabaseSchemas.IntegrationTests => "CREATE SCHEMA IF NOT EXISTS \"nexoratest\"",
            _ => throw Rejected(schema),
        };
        await database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static InvalidOperationException Rejected(string schema) => new(
        $"Refusing to operate on PostgreSQL schema '{schema}'. Nexora only owns "
        + $"'{DatabaseSchemas.Application}' (runtime) and '{DatabaseSchemas.IntegrationTests}' "
        + "(integration tests) inside the shared saas_dev database. This protects the "
        + "'dentalflow' and 'public' schemas.");
}
