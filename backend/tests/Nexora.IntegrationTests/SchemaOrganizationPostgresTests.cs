using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Persistence;
using Npgsql;

namespace Nexora.IntegrationTests;

/// <summary>
/// ADR-0020 — the domain tables live in per-context PostgreSQL schemas. Two paths are checked:
/// a database built from zero, and an incremental upgrade of a database that predates the schema
/// move (proving <c>ALTER TABLE ... SET SCHEMA</c> keeps every row, id and relationship).
/// Gated by NEXORA_HARDENING_POSTGRES, like the other PG suites.
/// </summary>
[Collection("Postgres")]
public sealed class SchemaOrganizationPostgresTests
{
    private const string BeforeSchemaMove = "20260902000000_SeedFeatureCatalog";

    private static readonly Dictionary<string, int> ExpectedTablesPerSchema = new()
    {
        ["identity"] = 6, ["tenancy"] = 4, ["platform"] = 2, ["plans"] = 4, ["billing"] = 6,
        ["customers"] = 1, ["catalog"] = 3, ["scheduling"] = 3, ["notifications"] = 1, ["onboarding"] = 1,
    };

    [Fact]
    public async Task AFreshDatabaseLandsEveryTableInItsContextSchemaAndLeavesPublicClean()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var perSchema = await ReadAsync(conn, """
            SELECT table_schema AS k, count(*)::int AS v FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
            GROUP BY table_schema
            """);
        foreach (var (schema, expected) in ExpectedTablesPerSchema)
            Assert.Equal(expected, (int)perSchema[schema]);
        Assert.Equal(31, ExpectedTablesPerSchema.Values.Sum());

        var publicTables = await ScalarListAsync(conn, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");
        Assert.Equal(["__EFMigrationsHistory"], publicTables);

        var crossSchemaFks = await ScalarAsync(conn, """
            SELECT count(*)::int FROM pg_constraint c
            JOIN pg_class cr ON cr.oid = c.conrelid JOIN pg_namespace cs ON cs.oid = cr.relnamespace
            JOIN pg_class tr ON tr.oid = c.confrelid JOIN pg_namespace ts ON ts.oid = tr.relnamespace
            WHERE c.contype = 'f' AND cs.nspname <> ts.nspname
            """);
        Assert.True((int)crossSchemaFks > 0, "cross-schema FKs should exist and be kept");

        // The composite (TenantId, X) FKs that block cross-tenant references must survive the move.
        var compositeFks = await ScalarAsync(conn, """
            SELECT count(*)::int FROM pg_constraint
            WHERE contype = 'f' AND array_length(conkey, 1) > 1
            """);
        Assert.Equal(7, (int)compositeFks);
    }

    [Fact]
    public async Task UpgradingADatabaseFromBeforeTheSchemaMovePreservesEveryRowAndRelationship()
    {
        var baseConnection = Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES");
        if (baseConnection is null) return;

        var dbName = $"nexora_schematest_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(baseConnection) { Database = "postgres" }.ConnectionString;
        var target = new NpgsqlConnectionStringBuilder(baseConnection) { Database = dbName }.ConnectionString;
        await ExecAsync(admin, $"CREATE DATABASE \"{dbName}\"");
        try
        {
            var options = new DbContextOptionsBuilder<NexoraDbContext>().UseNpgsql(target).Options;

            var ids = new
            {
                Tenant = Guid.NewGuid(), User = Guid.NewGuid(), Role = Guid.NewGuid(), Membership = Guid.NewGuid(),
                Customer = Guid.NewGuid(), Professional = Guid.NewGuid(), Service = Guid.NewGuid(), Appointment = Guid.NewGuid(),
                Plan = Guid.NewGuid(), Subscription = Guid.NewGuid(),
            };

            await using (var db = new NexoraDbContext(options))
            {
                // 1. Migrate up to (and including) the migration right before the schema move.
                await db.GetService<IMigrator>().MigrateAsync(BeforeSchemaMove);

                // 2. Seed a relationship-rich dataset while the tables are still in public.
                // Interpolated once into a plain string (ids are test-generated GUIDs, not user input).
                var seed = $"""
                    INSERT INTO tenants ("Id","Name","Slug","IsActive","CreatedAt","TimeZoneId")
                        VALUES ('{ids.Tenant}','Acme','acme',true,now(),'UTC');
                    INSERT INTO users ("Id","Email","NormalizedEmail","PasswordHash","IsActive","CreatedAt")
                        VALUES ('{ids.User}','a@nexora.test','A@NEXORA.TEST','x',true,now());
                    INSERT INTO tenant_roles ("Id","TenantId","Name","Description","IsSystem")
                        VALUES ('{ids.Role}','{ids.Tenant}','Admin','',true);
                    INSERT INTO tenant_users ("Id","TenantId","UserId","IsActive","CreatedAt","TenantRoleId")
                        VALUES ('{ids.Membership}','{ids.Tenant}','{ids.User}',true,now(),'{ids.Role}');
                    INSERT INTO customers ("Id","TenantId","Name","Status","CreatedAt","UpdatedAt")
                        VALUES ('{ids.Customer}','{ids.Tenant}','Cliente','Active',now(),now());
                    INSERT INTO professionals ("Id","TenantId","Name","IsActive")
                        VALUES ('{ids.Professional}','{ids.Tenant}','Profissional',true);
                    INSERT INTO services ("Id","TenantId","Name","DurationMinutes","Price","IsActive")
                        VALUES ('{ids.Service}','{ids.Tenant}','Corte',30,45,true);
                    INSERT INTO appointments ("Id","TenantId","CustomerId","ProfessionalId","ServiceId","StartAt","EndAt","Status","CreatedAt","UpdatedAt")
                        VALUES ('{ids.Appointment}','{ids.Tenant}','{ids.Customer}','{ids.Professional}','{ids.Service}',
                                now(),now() + interval '30 min','Scheduled',now(),now());
                    INSERT INTO plans ("Id","Code","Name","IsActive","CreatedAt","IsPublic","IsTrialEligible")
                        VALUES ('{ids.Plan}','PRO','Pro',true,now(),true,true);
                    INSERT INTO plan_features ("PlanId","FeatureId","Enabled","Limit")
                        SELECT '{ids.Plan}', "Id", true, null FROM features WHERE "Code" = 'CUSTOMERS';
                    INSERT INTO subscriptions ("Id","TenantId","PlanId","Status","BillingInterval","TrialStartAt","TrialEndAt",
                                "CurrentPeriodStart","CurrentPeriodEnd","CancelAtPeriodEnd","CreatedAt","UpdatedAt")
                        VALUES ('{ids.Subscription}','{ids.Tenant}','{ids.Plan}','Trialing','Monthly',now(),now() + interval '14 days',
                                now(),now() + interval '14 days',false,now(),now());
                    """;
                await db.Database.ExecuteSqlRawAsync(seed);
            }

            // 3. Apply the remaining migrations — this is the schema move.
            await using (var db = new NexoraDbContext(options))
                await db.Database.MigrateAsync();

            // 4. Every row is where it should be now, with the same id and the same relationships.
            await using var conn = new NpgsqlConnection(target);
            await conn.OpenAsync();

            Assert.Equal(dbName, conn.Database);
            Assert.Equal(1L, await ScalarAsync(conn, $"SELECT count(*) FROM tenancy.tenants WHERE \"Id\" = '{ids.Tenant}'"));
            Assert.Equal(1L, await ScalarAsync(conn, $"SELECT count(*) FROM identity.users WHERE \"Id\" = '{ids.User}'"));
            Assert.Equal(1L, await ScalarAsync(conn, $"SELECT count(*) FROM tenancy.tenant_users WHERE \"Id\" = '{ids.Membership}' AND \"TenantId\" = '{ids.Tenant}' AND \"UserId\" = '{ids.User}'"));
            Assert.Equal(1L, await ScalarAsync(conn, $"SELECT count(*) FROM billing.subscriptions WHERE \"Id\" = '{ids.Subscription}' AND \"PlanId\" = '{ids.Plan}' AND \"TenantId\" = '{ids.Tenant}'"));

            // The appointment still joins across schemas to its professional, customer and service.
            Assert.Equal(1L, await ScalarAsync(conn, $"""
                SELECT count(*) FROM scheduling.appointments a
                JOIN catalog.professionals p ON p."TenantId" = a."TenantId" AND p."Id" = a."ProfessionalId"
                JOIN customers.customers  c ON c."TenantId" = a."TenantId" AND c."Id" = a."CustomerId"
                JOIN catalog.services     s ON s."TenantId" = a."TenantId" AND s."Id" = a."ServiceId"
                WHERE a."Id" = '{ids.Appointment}'
                """));

            // The composite FK still rejects a cross-tenant professional reference.
            var otherTenant = Guid.NewGuid();
            await ExecOnAsync(conn, $"INSERT INTO tenancy.tenants (\"Id\",\"Name\",\"Slug\",\"IsActive\",\"CreatedAt\",\"TimeZoneId\") VALUES ('{otherTenant}','Other','other',true,now(),'UTC')");
            var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecOnAsync(conn, $"""
                INSERT INTO scheduling.working_hours ("Id","TenantId","ProfessionalId","DayOfWeek","StartLocal","EndLocal")
                VALUES ('{Guid.NewGuid()}','{otherTenant}','{ids.Professional}',1,'09:00','18:00')
                """));
            Assert.Equal("23503", violation.SqlState); // foreign_key_violation

            Assert.Equal(0L, await ScalarAsync(conn, "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name <> '__EFMigrationsHistory'"));
            var applied = await ScalarListAsync(conn, "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\"");
            Assert.Contains(BeforeSchemaMove, applied);
            Assert.Contains(applied, x => x.EndsWith("_OrganizeSchemasByContext", StringComparison.Ordinal));
        }
        finally
        {
            await ExecAsync(admin, $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)");
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await ExecOnAsync(conn, sql);
    }

    private static async Task ExecOnAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object> ScalarAsync(System.Data.Common.DbConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<List<string>> ScalarListAsync(System.Data.Common.DbConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static async Task<Dictionary<string, object>> ReadAsync(System.Data.Common.DbConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var result = new Dictionary<string, object>();
        while (await reader.ReadAsync()) result[reader.GetString(0)] = reader.GetValue(1);
        return result;
    }
}
