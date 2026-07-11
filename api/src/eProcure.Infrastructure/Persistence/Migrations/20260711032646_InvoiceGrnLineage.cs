using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceGrnLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrnId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            // T3 backfill (shared with, and covered by, InvoiceGrnLineageBackfillTests): link each
            // invoice to its receipt only where the invoice's PO has exactly one GRN. Ambiguous
            // (multiple partial receipts) and receipt-less invoices stay null — reported in the writeup.
            // The GrnId FK below tolerates null.
            foreach (var sql in InvoiceGrnLineageBackfill.Statements)
                migrationBuilder.Sql(sql);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_GrnId",
                table: "Invoices",
                column: "GrnId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Grns_GrnId",
                table: "Invoices",
                column: "GrnId",
                principalTable: "Grns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Grns_GrnId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_GrnId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "GrnId",
                table: "Invoices");
        }
    }
}
