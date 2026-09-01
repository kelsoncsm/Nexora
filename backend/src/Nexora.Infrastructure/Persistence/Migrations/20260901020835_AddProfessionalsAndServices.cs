using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalsAndServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "professionals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professionals", x => x.Id);
                    table.UniqueConstraint("AK_professionals_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_professionals_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.Id);
                    table.UniqueConstraint("AK_services_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_services_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "professional_services",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_professional_services", x => new { x.TenantId, x.ProfessionalId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_professional_services_professionals_TenantId_ProfessionalId",
                        columns: x => new { x.TenantId, x.ProfessionalId },
                        principalTable: "professionals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_professional_services_services_TenantId_ServiceId",
                        columns: x => new { x.TenantId, x.ServiceId },
                        principalTable: "services",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_professional_services_TenantId_ServiceId",
                table: "professional_services",
                columns: new[] { "TenantId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_professionals_TenantId_Name",
                table: "professionals",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_services_TenantId_Name",
                table: "services",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.Sql("""
                INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
                SELECT r."Id", p.permission FROM tenant_roles r
                CROSS JOIN (VALUES ('professionals.read'),('professionals.create'),('professionals.update'),('professionals.delete'),('services.read'),('services.create'),('services.update'),('services.delete')) AS p(permission)
                WHERE r."IsSystem" = true AND r."Name" = 'ADMIN'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM tenant_role_permissions WHERE "PermissionKey" IN ('professionals.read','professionals.create','professionals.update','professionals.delete','services.read','services.create','services.update','services.delete');""");
            migrationBuilder.DropTable(
                name: "professional_services");

            migrationBuilder.DropTable(
                name: "professionals");

            migrationBuilder.DropTable(
                name: "services");
        }
    }
}
