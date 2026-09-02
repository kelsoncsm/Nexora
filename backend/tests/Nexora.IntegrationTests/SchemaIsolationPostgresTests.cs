using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Npgsql;

namespace Nexora.IntegrationTests;

/// <summary>
/// ADR-0022 — the application owns a single schema (<c>nexora</c>) in the shared <c>saas_dev</c>
/// database and the integration suite is confined to <c>nexoratest</c>. These tests prove the
/// confinement: a full migrate lands every table in <c>nexoratest</c>, nothing leaks to
/// <c>nexora</c>/<c>public</c>, <c>dentalflow</c> is never touched, and the reset guard refuses
/// anything but <c>saas_dev.nexoratest</c>. Gated by NEXORA_HARDENING_POSTGRES.
/// </summary>
[Collection("Postgres")]
public sealed class SchemaIsolationPostgresTests
{
    private static string? BaseConnection => Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES");

    [Fact]
    public async Task MigratingConfinesEveryTableToNexoratestAndTouchesNothingElse()
    {
        if (BaseConnection is null) return;

        var dentalflowBefore = await FingerprintAsync("dentalflow");
        var nexoraBefore = await FingerprintAsync("nexora");

        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        await using var conn = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(BaseConnection) { SearchPath = "nexoratest" }.ConnectionString);
        await conn.OpenAsync();

        Assert.Equal("saas_dev", conn.Database);

        var perSchema = await DictAsync(conn, """
            SELECT table_schema AS k, count(*)::int AS v FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
            GROUP BY table_schema
            """);

        // 31 domain tables + __EFMigrationsHistory, all in nexoratest.
        Assert.Equal(32, GetOr0(perSchema, "nexoratest"));

        // The migration chain created nothing outside nexoratest: public has no base tables, and the
        // application schema is byte-for-byte what it was before the suite ran.
        var publicTables = await ListAsync(conn,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");
        Assert.Empty(publicTables);
        Assert.Equal(nexoraBefore, await FingerprintAsync("nexora"));

        // The history table is the nexoratest one, and it is complete.
        var applied = await ListAsync(conn, "SELECT \"MigrationId\" FROM nexoratest.\"__EFMigrationsHistory\"");
        Assert.Contains(applied, x => x.EndsWith("_AddAuditLogTenantAndDetails", StringComparison.Ordinal));
        Assert.DoesNotContain(applied, x => x.EndsWith("_OrganizeSchemasByContext", StringComparison.Ordinal));

        // audit_logs picked up the ADR-0021 columns inside nexoratest.
        var auditColumns = await ListAsync(conn,
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'nexoratest' AND table_name = 'audit_logs'");
        Assert.Contains("Details", auditColumns);
        Assert.Contains("TenantId", auditColumns);

        var dentalflowAfter = await FingerprintAsync("dentalflow");
        Assert.Equal(dentalflowBefore, dentalflowAfter);
    }

    [Fact]
    public void ResetGuardRejectsAnythingButSaasDevNexoratest()
    {
        var ok = "Host=localhost;Database=saas_dev;Username=u;Password=p;Search Path=nexoratest";
        TestSchemaGuard.EnsureIntegrationTestReset(ok, DatabaseSchemas.IntegrationTests); // no throw

        Assert.Throws<InvalidOperationException>(() => TestSchemaGuard.EnsureIntegrationTestReset(
            "Host=localhost;Database=nexora;Username=u;Password=p;Search Path=nexoratest", DatabaseSchemas.IntegrationTests));

        Assert.Throws<InvalidOperationException>(() => TestSchemaGuard.EnsureIntegrationTestReset(
            "Host=localhost;Database=saas_dev;Username=u;Password=p;Search Path=nexora", DatabaseSchemas.IntegrationTests));

        Assert.Throws<InvalidOperationException>(() => TestSchemaGuard.EnsureIntegrationTestReset(
            "Host=localhost;Database=saas_dev;Username=u;Password=p;Search Path=nexoratest", "nexora"));

        Assert.Throws<InvalidOperationException>(() => TestSchemaGuard.EnsureIntegrationTestReset(
            "Host=localhost;Database=saas_dev;Username=u;Password=p", DatabaseSchemas.IntegrationTests));
    }

    [Fact]
    public void RuntimeSchemaGuardRejectsSharedSchemas()
    {
        Assert.Equal("nexora", NexoraSchemaGuard.EnsureOwned("nexora"));
        Assert.Equal("nexoratest", NexoraSchemaGuard.EnsureOwned("nexoratest"));
        Assert.Throws<InvalidOperationException>(() => NexoraSchemaGuard.EnsureOwned("dentalflow"));
        Assert.Throws<InvalidOperationException>(() => NexoraSchemaGuard.EnsureOwned("public"));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static async Task<string> FingerprintAsync(string schema)
    {
        await using var conn = new NpgsqlConnection(BaseConnection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT coalesce(string_agg(t || ':' || n, ',' ORDER BY t), '<empty>') FROM (
              SELECT c.relname AS t,
                     (xpath('/row/c/text()', query_to_xml(format('select count(*) c from %I.%I', '{schema}', c.relname), false, true, '')))[1]::text::int AS n
              FROM pg_class c JOIN pg_namespace ns ON ns.oid = c.relnamespace
              WHERE ns.nspname = '{schema}' AND c.relkind = 'r'
            ) s
            """;
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Dictionary<string, object>> DictAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new Dictionary<string, object>();
        while (await reader.ReadAsync()) result[reader.GetString(0)] = reader.GetValue(1);
        return result;
    }

    private static int GetOr0(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? (int)v : 0;

    private static async Task<List<string>> ListAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }
}
