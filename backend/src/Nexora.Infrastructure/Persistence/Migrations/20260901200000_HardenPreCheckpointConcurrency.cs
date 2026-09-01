using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenPreCheckpointConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_invoices_SubscriptionId",
                table: "billing_invoices");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CoverageEnd",
                table: "billing_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CoverageStart",
                table: "billing_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM billing_invoices i
                        JOIN subscriptions s ON s."Id" = i."SubscriptionId"
                        WHERE s."Status" <> 'Trialing') THEN
                        RAISE EXCEPTION 'Cannot safely backfill billing invoice coverage for a non-trialing historical subscription.';
                    END IF;
                END $$;

                UPDATE billing_invoices i
                SET "CoverageStart" = s."TrialEndAt",
                    "CoverageEnd" = CASE s."BillingInterval"
                        WHEN 'Monthly' THEN s."TrialEndAt" + INTERVAL '1 month'
                        WHEN 'Yearly' THEN s."TrialEndAt" + INTERVAL '1 year'
                    END
                FROM subscriptions s
                WHERE s."Id" = i."SubscriptionId";
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(name:"CoverageEnd",table:"billing_invoices",type:"timestamp with time zone",nullable:false,oldClrType:typeof(DateTimeOffset),oldType:"timestamp with time zone",oldNullable:true);
            migrationBuilder.AlterColumn<DateTimeOffset>(name:"CoverageStart",table:"billing_invoices",type:"timestamp with time zone",nullable:false,oldClrType:typeof(DateTimeOffset),oldType:"timestamp with time zone",oldNullable:true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_invoices_SubscriptionId_CoverageStart_CoverageEnd",
                table: "billing_invoices",
                columns: new[] { "SubscriptionId", "CoverageStart", "CoverageEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_invoices_SubscriptionId_CoverageStart_CoverageEnd",
                table: "billing_invoices");

            migrationBuilder.DropColumn(
                name: "CoverageEnd",
                table: "billing_invoices");

            migrationBuilder.DropColumn(
                name: "CoverageStart",
                table: "billing_invoices");

            migrationBuilder.CreateIndex(
                name: "IX_billing_invoices_SubscriptionId",
                table: "billing_invoices",
                column: "SubscriptionId");
        }
    }
}
