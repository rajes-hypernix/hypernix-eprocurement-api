using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class RequisitionShipToLineTaxSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipToAddressId",
                schema: "sourcing",
                table: "PurchaseRequisitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipToAdhoc",
                schema: "sourcing",
                table: "PurchaseRequisitions",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShipToLocationId",
                schema: "sourcing",
                table: "PurchaseRequisitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineSequence",
                schema: "sourcing",
                table: "PrLines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxCodeId",
                schema: "sourcing",
                table: "PrLines",
                type: "uuid",
                nullable: true);

            // Existing lines all got 0 — renumber 1..n per PR before the unique index.
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "PurchaseRequisitionId", "TenantId" ORDER BY "Id") AS seq
                    FROM sourcing."PrLines"
                )
                UPDATE sourcing."PrLines" AS p
                SET "LineSequence" = ranked.seq
                FROM ranked
                WHERE p."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_Pr_LineSequence",
                schema: "sourcing",
                table: "PrLines",
                columns: new[] { "PurchaseRequisitionId", "LineSequence", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrLines_Pr_LineSequence",
                schema: "sourcing",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "ShipToAddressId",
                schema: "sourcing",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ShipToAdhoc",
                schema: "sourcing",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "ShipToLocationId",
                schema: "sourcing",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LineSequence",
                schema: "sourcing",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "TaxCodeId",
                schema: "sourcing",
                table: "PrLines");
        }
    }
}
