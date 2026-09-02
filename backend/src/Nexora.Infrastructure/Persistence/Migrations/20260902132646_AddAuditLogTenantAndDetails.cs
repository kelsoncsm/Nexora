using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P2.9 / ADR-0021 — adds <c>Details</c> (jsonb, nullable) and <c>TenantId</c> (uuid, nullable,
    /// no FK) to <c>audit_logs</c>. Purely additive; existing rows get NULL for both.
    /// </summary>
    /// <remarks>
    /// <para><b>Baseline suppression (ADR-0022).</b> When this migration was scaffolded the EF model
    /// had just adopted <c>HasDefaultSchema("nexora")</c>, so the diff also produced an
    /// <c>EnsureSchema("nexora")</c> plus 31 <c>RenameTable</c> (<c>public</c> → <c>nexora</c>).
    /// Those schema-relocation operations were removed on purpose:</para>
    /// <list type="bullet">
    ///   <item>every existing <c>saas_dev</c> database was physically moved to the application
    ///     schema <c>nexora</c> before this migration, so re-running the move would fail
    ///     (<c>table ... is already in schema "nexora"</c>);</item>
    ///   <item>the historical migrations stay schema-relative (unqualified names resolved via
    ///     <c>search_path</c>), so the same chain still initializes a fresh <c>nexora</c> or
    ///     <c>nexoratest</c> — the schema is pre-created by the host before <c>MigrateAsync</c>.</item>
    /// </list>
    /// <para>The regenerated <c>.Designer.cs</c> / model snapshot keep the final shape
    /// (default schema <c>nexora</c>, all tables in it) as the baseline for future migrations.
    /// This file was edited deliberately — it is not an accidental hand-patch.</para>
    /// </remarks>
    /// <inheritdoc />
    public partial class AddAuditLogTenantAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema on the operation: resolved through search_path, so the same migration
            // targets nexora (app) or nexoratest (integration tests).
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "audit_logs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "audit_logs");
        }
    }
}
