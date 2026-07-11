using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AwardPoLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AwardId",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AwardAllocationId",
                table: "PoLines",
                type: "uuid",
                nullable: true);

            // AN-2 backfill (shared with, and covered by, AwardPoLineageBackfillTests): PO → Award via
            // the legacy AwardCode; PO line → its AwardAllocation where the match is unique. Unmatched /
            // ambiguous rows stay null and are reported in the slice writeup. The AwardId FK below
            // tolerates null.
            foreach (var sql in AwardPoLineageBackfill.Statements)
                migrationBuilder.Sql(sql);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_AwardId",
                table: "PurchaseOrders",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_AwardAllocationId",
                table: "PoLines",
                column: "AwardAllocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Awards_AwardId",
                table: "PurchaseOrders",
                column: "AwardId",
                principalTable: "Awards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Awards_AwardId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_AwardId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PoLines_AwardAllocationId",
                table: "PoLines");

            migrationBuilder.DropColumn(
                name: "AwardId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AwardAllocationId",
                table: "PoLines");
        }
    }
}
