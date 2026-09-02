namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// The Nexora application owns a single PostgreSQL schema inside the shared <c>saas_dev</c>
/// database (ADR-0022, supersedes ADR-0020). Bounded contexts stay a logical concern of the
/// codebase — they no longer map to physical schemas. The default schema is
/// <see cref="Application"/>; the integration-test host overrides it to <see cref="IntegrationTests"/>
/// so its migrations and data never touch the application schema. The <c>dentalflow</c> schema in
/// the same database belongs to another system and must never be touched.
/// </summary>
public static class DatabaseSchemas
{
    /// <summary>Schema used by the running application (dev, staging, production).</summary>
    public const string Application = "nexora";

    /// <summary>Schema the integration-test suite is confined to. Reset per run; never the app schema.</summary>
    public const string IntegrationTests = "nexoratest";

    /// <summary>EF Core migrations-history table name; lives in whichever schema is active.</summary>
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";
}
