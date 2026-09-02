using Nexora.Infrastructure.Persistence;
using Npgsql;

namespace Nexora.IntegrationTests;

/// <summary>
/// The PostgreSQL integration suite runs against the shared <c>saas_dev</c> database — the same one
/// that holds the application's <c>nexora</c> schema and DentalFlow's <c>dentalflow</c> schema.
/// Every destructive reset must pass through here first. It aborts unless the target is exactly
/// <c>saas_dev.nexoratest</c>, so a stray connection string or configuration can never drop or
/// migrate the application schema, <c>dentalflow</c>, or <c>public</c>.
/// </summary>
internal static class TestSchemaGuard
{
    private const string RequiredDatabase = "saas_dev";

    /// <summary>
    /// Validates the connection and configured schema before a <c>DROP SCHEMA nexoratest CASCADE</c>.
    /// Throws with a precise message on any mismatch.
    /// </summary>
    public static void EnsureIntegrationTestReset(string connectionString, string configuredSchema)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        Require(b.Database == RequiredDatabase, "Database", b.Database, RequiredDatabase);
        Require(b.SearchPath == DatabaseSchemas.IntegrationTests, "Search Path", b.SearchPath, DatabaseSchemas.IntegrationTests);
        Require(configuredSchema == DatabaseSchemas.IntegrationTests, "configured schema", configuredSchema, DatabaseSchemas.IntegrationTests);
    }

    private static void Require(bool ok, string field, string? actual, string expected)
    {
        if (!ok)
            throw new InvalidOperationException(
                $"ABORT integration-test schema reset: {field} must be '{expected}', got '{actual ?? "<null>"}'. "
                + "The integration suite is only ever allowed to touch saas_dev.nexoratest.");
    }
}
