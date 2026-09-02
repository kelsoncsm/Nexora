using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P2.9 / ADR-0021 — adds <c>TenantId</c> (nullable, no FK) and <c>Details</c> (jsonb, nullable)
    /// to <c>platform.audit_logs</c>. Purely additive: no data loss, no table move or rename, schema
    /// stays <c>platform</c> (ADR-0020). Existing rows get NULL for both.
    /// </summary>
    /// <inheritdoc />
    public partial class AddAuditLogTenantAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Details",
                schema: "platform",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "platform",
                table: "audit_logs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Details",
                schema: "platform",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "platform",
                table: "audit_logs");
        }
    }
}
