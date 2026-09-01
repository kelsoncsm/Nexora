using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReportsAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
                SELECT "Id", 'reports.read'
                FROM tenant_roles
                WHERE "IsSystem" = TRUE
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM tenant_role_permissions WHERE \"PermissionKey\" = 'reports.read';");
        }
    }
}
