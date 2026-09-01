using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfServiceOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SegmentId",
                table: "tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrialEligible",
                table: "plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "onboarding_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompanySlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SegmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingInterval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_onboarding_drafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_onboarding_drafts_business_segments_SegmentId",
                        column: x => x.SegmentId,
                        principalTable: "business_segments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_drafts_plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_drafts_tenants_CompletedTenantId",
                        column: x => x.CompletedTenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_onboarding_drafts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_SegmentId",
                table: "tenants",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_drafts_CompletedTenantId",
                table: "onboarding_drafts",
                column: "CompletedTenantId",
                unique: true,
                filter: "\"CompletedTenantId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_drafts_PlanId",
                table: "onboarding_drafts",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_drafts_SegmentId",
                table: "onboarding_drafts",
                column: "SegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_onboarding_drafts_UserId_Status",
                table: "onboarding_drafts",
                columns: new[] { "UserId", "Status" },
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.AddForeignKey(
                name: "FK_tenants_business_segments_SegmentId",
                table: "tenants",
                column: "SegmentId",
                principalTable: "business_segments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenants_business_segments_SegmentId",
                table: "tenants");

            migrationBuilder.DropTable(
                name: "onboarding_drafts");

            migrationBuilder.DropIndex(
                name: "IX_tenants_SegmentId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SegmentId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "plans");

            migrationBuilder.DropColumn(
                name: "IsTrialEligible",
                table: "plans");
        }
    }
}
