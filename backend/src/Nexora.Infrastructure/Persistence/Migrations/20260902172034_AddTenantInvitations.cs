using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the <c>tenant_invitations</c> table for the tenant member invitation workflow
    /// (approved 2026-09-02). Purely additive — one new table, no changes to existing objects.
    /// </summary>
    /// <remarks>
    /// Schema-relative on purpose (ADR-0022): the operations carry no <c>schema:</c> argument so the
    /// unqualified names resolve through <c>search_path</c>. The one migration chain therefore
    /// targets <c>nexora</c> (app) or <c>nexoratest</c> (integration tests) depending on which schema
    /// the host pre-creates and points the search path at. The regenerated <c>.Designer.cs</c> /
    /// model snapshot keep the baseline default schema (<c>nexora</c>); that is the model shape, not
    /// a hard-coded target. This file was edited deliberately after scaffolding.
    /// </remarks>
    /// <inheritdoc />
    public partial class AddTenantInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TenantRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_invitations_tenant_roles_TenantRoleId",
                        column: x => x.TenantRoleId,
                        principalTable: "tenant_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenant_invitations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_TenantId_NormalizedEmail",
                table: "tenant_invitations",
                columns: new[] { "TenantId", "NormalizedEmail" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_TenantRoleId",
                table: "tenant_invitations",
                column: "TenantRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_invitations_TokenHash",
                table: "tenant_invitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_invitations");
        }
    }
}
