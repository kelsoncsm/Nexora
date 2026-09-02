using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexora.Application.Billing;
using Nexora.Infrastructure.Persistence;
using Npgsql;

namespace Nexora.IntegrationTests;

/// <summary>
/// Hosts the API against the shared <c>saas_dev</c> database, confined to the <c>nexoratest</c>
/// schema (ADR-0022). Whatever <c>NEXORA_HARDENING_POSTGRES</c> carries, the search path and the
/// configured schema are forced to <c>nexoratest</c>, and <see cref="InitializeAsync"/> drops and
/// recreates only that schema — after <see cref="TestSchemaGuard"/> validates the target.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    public FakePaymentGateway PaymentGateway { get; } = new();

    public PostgresApiFactory()
    {
        var raw = Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES")
            ?? throw new InvalidOperationException("NEXORA_HARDENING_POSTGRES is required.");
        connectionString = new NpgsqlConnectionStringBuilder(raw)
        {
            SearchPath = DatabaseSchemas.IntegrationTests,
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:NexoraDatabase", connectionString);
        builder.UseSetting("Database:Schema", DatabaseSchemas.IntegrationTests);
        builder.UseSetting("Identity:Issuer", "Nexora.Postgres.Tests");
        builder.UseSetting("Identity:Audience", "Nexora.Postgres.Tests.Client");
        builder.UseSetting("Identity:SigningKey", "postgres-integration-test-key-with-more-than-32-characters");
        builder.UseSetting("Payments:MercadoPago:WebhookSecret", "test-webhook-secret");
        builder.UseSetting("Email:Provider", "Fake");
        builder.UseSetting("Email:WorkerEnabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NexoraDbContext>();
            services.RemoveAll<DbContextOptions<NexoraDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            services.AddDbContext<NexoraDbContext>(options => options
                .UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable(DatabaseSchemas.MigrationsHistoryTable, DatabaseSchemas.IntegrationTests))
                // The one migration chain is deployed to nexora (app) and nexoratest (this suite);
                // only the runtime default schema differs, which EF flags as a pending model change.
                // The real drift check stays in CI via `dotnet ef migrations has-pending-model-changes`.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(PaymentGateway);
        });
    }

    public async Task InitializeAsync()
    {
        TestSchemaGuard.EnsureIntegrationTestReset(connectionString, DatabaseSchemas.IntegrationTests);

        await using (var admin = new NpgsqlConnection(connectionString))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            // Guarded above to saas_dev.nexoratest. Never touches nexora / dentalflow / public.
            cmd.CommandText = "DROP SCHEMA IF EXISTS nexoratest CASCADE; CREATE SCHEMA nexoratest;";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().Database.MigrateAsync();
    }
}
