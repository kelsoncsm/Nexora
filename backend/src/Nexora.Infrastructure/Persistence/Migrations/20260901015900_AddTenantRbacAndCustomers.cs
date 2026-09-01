using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRbacAndCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantRoleId",
                table: "tenant_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_roles_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_role_permissions",
                columns: table => new
                {
                    TenantRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_role_permissions", x => new { x.TenantRoleId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_tenant_role_permissions_tenant_roles_TenantRoleId",
                        column: x => x.TenantRoleId,
                        principalTable: "tenant_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO tenant_roles ("Id", "TenantId", "Name", "Description", "IsSystem")
                SELECT gen_random_uuid(), "TenantId", upper("Role"), 'Migrated tenant role', true
                FROM tenant_users GROUP BY "TenantId", upper("Role");
                INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
                SELECT "Id", permission FROM tenant_roles
                CROSS JOIN (VALUES ('customers.read'),('customers.create'),('customers.update'),('customers.delete')) AS p(permission);
                UPDATE tenant_users u SET "TenantRoleId" = r."Id"
                FROM tenant_roles r WHERE r."TenantId" = u."TenantId" AND r."Name" = upper(u."Role");
                """);

            migrationBuilder.AlterColumn<Guid>(name: "TenantRoleId", table: "tenant_users", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.DropColumn(name: "Role", table: "tenant_users");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantRoleId",
                table: "tenant_users",
                column: "TenantRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Name",
                table: "customers",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Status",
                table: "customers",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_roles_TenantId_Name",
                table: "tenant_roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_users_tenant_roles_TenantRoleId",
                table: "tenant_users",
                column: "TenantRoleId",
                principalTable: "tenant_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Role", table: "tenant_users", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.Sql("""UPDATE tenant_users u SET "Role" = r."Name" FROM tenant_roles r WHERE r."Id" = u."TenantRoleId";""");
            migrationBuilder.AlterColumn<string>(name: "Role", table: "tenant_users", type: "character varying(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_users_tenant_roles_TenantRoleId",
                table: "tenant_users");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "tenant_role_permissions");

            migrationBuilder.DropTable(
                name: "tenant_roles");

            migrationBuilder.DropIndex(
                name: "IX_tenant_users_TenantRoleId",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "TenantRoleId",
                table: "tenant_users");

        }
    }
}
