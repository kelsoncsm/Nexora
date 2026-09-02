namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL schema per bounded context (ADR-0020). Every <c>IEntityTypeConfiguration</c> pins its
/// tables to one of these; there is no <c>HasDefaultSchema</c>. <c>__EFMigrationsHistory</c> stays in
/// <c>public</c>. Adding a schema here is an architectural decision, not configuration.
/// </summary>
public static class DatabaseSchemas
{
    public const string Identity = "identity";
    public const string Tenancy = "tenancy";
    public const string Platform = "platform";
    public const string Plans = "plans";
    public const string Billing = "billing";
    public const string Customers = "customers";
    public const string Catalog = "catalog";
    public const string Scheduling = "scheduling";
    public const string Notifications = "notifications";
    public const string Onboarding = "onboarding";

    /// <summary>Every context schema, in a stable order. Used by the schema-move migration and its tests.</summary>
    public static readonly string[] All =
        [Identity, Tenancy, Platform, Plans, Billing, Customers, Catalog, Scheduling, Notifications, Onboarding];
}
