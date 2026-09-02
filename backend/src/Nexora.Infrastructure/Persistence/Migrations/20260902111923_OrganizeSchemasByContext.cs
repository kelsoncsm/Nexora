using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves every domain table from <c>public</c> into a per-bounded-context schema (ADR-0020).
    /// Each <c>RenameTable</c> emits <c>ALTER TABLE &lt;t&gt; SET SCHEMA &lt;s&gt;</c> — atomic and
    /// non-destructive: data, PK, FKs (in and out), indexes, unique/partial constraints and OIDs are
    /// preserved; no DROP/CREATE, no sequences to move (all PKs are client-generated uuid).
    /// <c>__EFMigrationsHistory</c> stays in <c>public</c>. Works both from an empty database (the
    /// historical migrations create the tables in public first, then this moves them) and as an
    /// incremental upgrade of an existing database. <c>Down()</c> moves everything back and drops the
    /// now-empty schemas.
    /// </summary>
    /// <inheritdoc />
    public partial class OrganizeSchemasByContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "scheduling");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.EnsureSchema(
                name: "plans");

            migrationBuilder.EnsureSchema(
                name: "onboarding");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.RenameTable(
                name: "working_hours",
                newName: "working_hours",
                newSchema: "scheduling");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "user_roles",
                newName: "user_roles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "tenants",
                newName: "tenants",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "tenant_users",
                newName: "tenant_users",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "tenant_roles",
                newName: "tenant_roles",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "tenant_role_permissions",
                newName: "tenant_role_permissions",
                newSchema: "tenancy");

            migrationBuilder.RenameTable(
                name: "tenant_feature_overrides",
                newName: "tenant_feature_overrides",
                newSchema: "plans");

            migrationBuilder.RenameTable(
                name: "subscriptions",
                newName: "subscriptions",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "subscription_events",
                newName: "subscription_events",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "services",
                newName: "services",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "roles",
                newName: "roles",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "role_permissions",
                newName: "role_permissions",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                newName: "refresh_tokens",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "professionals",
                newName: "professionals",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "professional_services",
                newName: "professional_services",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "processed_webhook_events",
                newName: "processed_webhook_events",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "plans",
                newName: "plans",
                newSchema: "plans");

            migrationBuilder.RenameTable(
                name: "plan_prices",
                newName: "plan_prices",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "plan_features",
                newName: "plan_features",
                newSchema: "plans");

            migrationBuilder.RenameTable(
                name: "permissions",
                newName: "permissions",
                newSchema: "identity");

            migrationBuilder.RenameTable(
                name: "onboarding_drafts",
                newName: "onboarding_drafts",
                newSchema: "onboarding");

            migrationBuilder.RenameTable(
                name: "features",
                newName: "features",
                newSchema: "plans");

            migrationBuilder.RenameTable(
                name: "email_outbox_messages",
                newName: "email_outbox_messages",
                newSchema: "notifications");

            migrationBuilder.RenameTable(
                name: "customers",
                newName: "customers",
                newSchema: "customers");

            migrationBuilder.RenameTable(
                name: "business_segments",
                newName: "business_segments",
                newSchema: "platform");

            migrationBuilder.RenameTable(
                name: "blocked_periods",
                newName: "blocked_periods",
                newSchema: "scheduling");

            migrationBuilder.RenameTable(
                name: "billing_payments",
                newName: "billing_payments",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "billing_invoices",
                newName: "billing_invoices",
                newSchema: "billing");

            migrationBuilder.RenameTable(
                name: "audit_logs",
                newName: "audit_logs",
                newSchema: "platform");

            migrationBuilder.RenameTable(
                name: "appointments",
                newName: "appointments",
                newSchema: "scheduling");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Move every table back to public with an explicit newSchema (EF's scaffolded
            // reversal omits it, which no-ops), then drop the now-empty context schemas.
            migrationBuilder.RenameTable(
                name: "users",
                schema: "identity",
                newName: "users",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "roles",
                schema: "identity",
                newName: "roles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "permissions",
                schema: "identity",
                newName: "permissions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "role_permissions",
                schema: "identity",
                newName: "role_permissions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "user_roles",
                schema: "identity",
                newName: "user_roles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "refresh_tokens",
                schema: "identity",
                newName: "refresh_tokens",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenants",
                schema: "tenancy",
                newName: "tenants",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenant_users",
                schema: "tenancy",
                newName: "tenant_users",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenant_roles",
                schema: "tenancy",
                newName: "tenant_roles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenant_role_permissions",
                schema: "tenancy",
                newName: "tenant_role_permissions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "business_segments",
                schema: "platform",
                newName: "business_segments",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "audit_logs",
                schema: "platform",
                newName: "audit_logs",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "features",
                schema: "plans",
                newName: "features",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "plans",
                schema: "plans",
                newName: "plans",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "plan_features",
                schema: "plans",
                newName: "plan_features",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "tenant_feature_overrides",
                schema: "plans",
                newName: "tenant_feature_overrides",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "subscriptions",
                schema: "billing",
                newName: "subscriptions",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "subscription_events",
                schema: "billing",
                newName: "subscription_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "plan_prices",
                schema: "billing",
                newName: "plan_prices",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "billing_invoices",
                schema: "billing",
                newName: "billing_invoices",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "billing_payments",
                schema: "billing",
                newName: "billing_payments",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "processed_webhook_events",
                schema: "billing",
                newName: "processed_webhook_events",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "customers",
                schema: "customers",
                newName: "customers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "professionals",
                schema: "catalog",
                newName: "professionals",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "services",
                schema: "catalog",
                newName: "services",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "professional_services",
                schema: "catalog",
                newName: "professional_services",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "working_hours",
                schema: "scheduling",
                newName: "working_hours",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "blocked_periods",
                schema: "scheduling",
                newName: "blocked_periods",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "appointments",
                schema: "scheduling",
                newName: "appointments",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "email_outbox_messages",
                schema: "notifications",
                newName: "email_outbox_messages",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "onboarding_drafts",
                schema: "onboarding",
                newName: "onboarding_drafts",
                newSchema: "public");

            migrationBuilder.DropSchema(name: "onboarding");
            migrationBuilder.DropSchema(name: "notifications");
            migrationBuilder.DropSchema(name: "scheduling");
            migrationBuilder.DropSchema(name: "catalog");
            migrationBuilder.DropSchema(name: "customers");
            migrationBuilder.DropSchema(name: "billing");
            migrationBuilder.DropSchema(name: "plans");
            migrationBuilder.DropSchema(name: "platform");
            migrationBuilder.DropSchema(name: "tenancy");
            migrationBuilder.DropSchema(name: "identity");
        }
    }
}
