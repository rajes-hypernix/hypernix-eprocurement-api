using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransitionTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AwardedUtc",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedUtc",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedUtc",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedUtc",
                table: "PurchaseRequisitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedUtc",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedUtc",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedUtc",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedUtc",
                table: "Asns",
                type: "timestamp with time zone",
                nullable: true);

            // AN-3 backfill (shared with, and covered by, TransitionTimestampsBackfillTests): populate
            // only the columns with a CLEAN recorded source (RFQ release/close from RfqEvent, PR submit
            // from the typed AuditEntry). Everything else stays null — never fabricated. See
            // TransitionTimestampsBackfill for the full clean/null split.
            foreach (var sql in TransitionTimestampsBackfill.Statements)
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwardedUtc",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "ClosedUtc",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "ReleasedUtc",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "SubmittedUtc",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "AcknowledgedUtc",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IssuedUtc",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "SubmittedUtc",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReceivedUtc",
                table: "Asns");
        }
    }
}
