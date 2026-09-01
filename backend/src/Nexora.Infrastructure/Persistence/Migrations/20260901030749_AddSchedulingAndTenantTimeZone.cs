using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulingAndTenantTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.Sql("ALTER TABLE tenants ALTER COLUMN \"TimeZoneId\" DROP DEFAULT;");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_customers_TenantId_Id",
                table: "customers",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointments_customers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalTable: "customers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_professionals_TenantId_ProfessionalId",
                        columns: x => new { x.TenantId, x.ProfessionalId },
                        principalTable: "professionals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_appointments_services_TenantId_ServiceId",
                        columns: x => new { x.TenantId, x.ServiceId },
                        principalTable: "services",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blocked_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_blocked_periods_professionals_TenantId_ProfessionalId",
                        columns: x => new { x.TenantId, x.ProfessionalId },
                        principalTable: "professionals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartLocal = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndLocal = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_working_hours_professionals_TenantId_ProfessionalId",
                        columns: x => new { x.TenantId, x.ProfessionalId },
                        principalTable: "professionals",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_TenantId_CustomerId",
                table: "appointments",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_TenantId_ProfessionalId_StartAt",
                table: "appointments",
                columns: new[] { "TenantId", "ProfessionalId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_TenantId_ServiceId",
                table: "appointments",
                columns: new[] { "TenantId", "ServiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_TenantId_StartAt",
                table: "appointments",
                columns: new[] { "TenantId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_periods_TenantId_ProfessionalId_StartAt",
                table: "blocked_periods",
                columns: new[] { "TenantId", "ProfessionalId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_working_hours_TenantId_ProfessionalId_DayOfWeek",
                table: "working_hours",
                columns: new[] { "TenantId", "ProfessionalId", "DayOfWeek" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
                SELECT tr."Id", permission_key
                FROM tenant_roles tr
                CROSS JOIN (VALUES
                    ('appointments.read'),
                    ('appointments.create'),
                    ('appointments.update'),
                    ('appointments.cancel')
                ) AS permissions(permission_key)
                WHERE tr."IsSystem" = TRUE AND tr."Name" = 'ADMIN'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM tenant_role_permissions
                WHERE "PermissionKey" IN ('appointments.read', 'appointments.create', 'appointments.update', 'appointments.cancel');
                """);
            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "blocked_periods");

            migrationBuilder.DropTable(
                name: "working_hours");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_customers_TenantId_Id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "tenants");
        }
    }
}
