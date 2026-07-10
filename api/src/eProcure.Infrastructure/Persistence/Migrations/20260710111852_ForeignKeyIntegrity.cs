using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForeignKeyIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-flight (DBA-1): a business table with a dangling reference cannot be constrained.
            // Detect any pre-existing orphan BEFORE creating constraints and abort with a precise
            // message, rather than letting AddForeignKey fail opaquely in production. (Nullable child
            // keys — e.g. PurchaseOrders.RfqId — are excluded; a null reference is not an orphan.)
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_orphans text := '';
    v_count   bigint;
BEGIN
    SELECT count(*) INTO v_count FROM ""Asns"" c LEFT JOIN ""PurchaseOrders"" p ON c.""PoId"" = p.""Id"" WHERE c.""PoId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Asns.PoId->PurchaseOrders: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Asns"" c LEFT JOIN ""Vendors"" p ON c.""VendorId"" = p.""Id"" WHERE c.""VendorId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Asns.VendorId->Vendors: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""AwardAllocations"" c LEFT JOIN ""Vendors"" p ON c.""VendorId"" = p.""Id"" WHERE c.""VendorId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('AwardAllocations.VendorId->Vendors: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Awards"" c LEFT JOIN ""Rfqs"" p ON c.""RfqId"" = p.""Id"" WHERE c.""RfqId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Awards.RfqId->Rfqs: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Bids"" c LEFT JOIN ""Rfqs"" p ON c.""RfqId"" = p.""Id"" WHERE c.""RfqId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Bids.RfqId->Rfqs: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Bids"" c LEFT JOIN ""Vendors"" p ON c.""VendorId"" = p.""Id"" WHERE c.""VendorId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Bids.VendorId->Vendors: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Grns"" c LEFT JOIN ""Asns"" p ON c.""AsnId"" = p.""Id"" WHERE c.""AsnId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Grns.AsnId->Asns: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Grns"" c LEFT JOIN ""PurchaseOrders"" p ON c.""PoId"" = p.""Id"" WHERE c.""PoId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Grns.PoId->PurchaseOrders: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Invoices"" c LEFT JOIN ""PurchaseOrders"" p ON c.""PoId"" = p.""Id"" WHERE c.""PoId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Invoices.PoId->PurchaseOrders: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""Invoices"" c LEFT JOIN ""Vendors"" p ON c.""VendorId"" = p.""Id"" WHERE c.""VendorId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('Invoices.VendorId->Vendors: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""PrLineSourcings"" c LEFT JOIN ""Rfqs"" p ON c.""RfqId"" = p.""Id"" WHERE c.""RfqId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('PrLineSourcings.RfqId->Rfqs: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""PurchaseOrders"" c LEFT JOIN ""Rfqs"" p ON c.""RfqId"" = p.""Id"" WHERE c.""RfqId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('PurchaseOrders.RfqId->Rfqs: %s; ', v_count); END IF;
    SELECT count(*) INTO v_count FROM ""PurchaseOrders"" c LEFT JOIN ""Vendors"" p ON c.""VendorId"" = p.""Id"" WHERE c.""VendorId"" IS NOT NULL AND p.""Id"" IS NULL;
    IF v_count > 0 THEN v_orphans := v_orphans || format('PurchaseOrders.VendorId->Vendors: %s; ', v_count); END IF;

    IF v_orphans <> '' THEN
        RAISE EXCEPTION 'ForeignKeyIntegrity aborted: pre-existing orphan rows must be resolved before FK constraints can be added -> %', v_orphans;
    END IF;
END $$;");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_RfqId",
                table: "PurchaseOrders",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_VendorId",
                table: "PurchaseOrders",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PoId",
                table: "Invoices",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoices",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_AsnId",
                table: "Grns",
                column: "AsnId");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_PoId",
                table: "Grns",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_VendorId",
                table: "Bids",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_AwardAllocations_VendorId",
                table: "AwardAllocations",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Asns_PoId",
                table: "Asns",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_Asns_VendorId",
                table: "Asns",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Asns_PurchaseOrders_PoId",
                table: "Asns",
                column: "PoId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Asns_Vendors_VendorId",
                table: "Asns",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AwardAllocations_Vendors_VendorId",
                table: "AwardAllocations",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Awards_Rfqs_RfqId",
                table: "Awards",
                column: "RfqId",
                principalTable: "Rfqs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Rfqs_RfqId",
                table: "Bids",
                column: "RfqId",
                principalTable: "Rfqs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Vendors_VendorId",
                table: "Bids",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Asns_AsnId",
                table: "Grns",
                column: "AsnId",
                principalTable: "Asns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_PurchaseOrders_PoId",
                table: "Grns",
                column: "PoId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_PurchaseOrders_PoId",
                table: "Invoices",
                column: "PoId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Vendors_VendorId",
                table: "Invoices",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrLineSourcings_Rfqs_RfqId",
                table: "PrLineSourcings",
                column: "RfqId",
                principalTable: "Rfqs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Rfqs_RfqId",
                table: "PurchaseOrders",
                column: "RfqId",
                principalTable: "Rfqs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Vendors_VendorId",
                table: "PurchaseOrders",
                column: "VendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Asns_PurchaseOrders_PoId",
                table: "Asns");

            migrationBuilder.DropForeignKey(
                name: "FK_Asns_Vendors_VendorId",
                table: "Asns");

            migrationBuilder.DropForeignKey(
                name: "FK_AwardAllocations_Vendors_VendorId",
                table: "AwardAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Awards_Rfqs_RfqId",
                table: "Awards");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Rfqs_RfqId",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Vendors_VendorId",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Asns_AsnId",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_PurchaseOrders_PoId",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_PurchaseOrders_PoId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Vendors_VendorId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PrLineSourcings_Rfqs_RfqId",
                table: "PrLineSourcings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Rfqs_RfqId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Vendors_VendorId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_RfqId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_VendorId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PoId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_VendorId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Grns_AsnId",
                table: "Grns");

            migrationBuilder.DropIndex(
                name: "IX_Grns_PoId",
                table: "Grns");

            migrationBuilder.DropIndex(
                name: "IX_Bids_VendorId",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_AwardAllocations_VendorId",
                table: "AwardAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Asns_PoId",
                table: "Asns");

            migrationBuilder.DropIndex(
                name: "IX_Asns_VendorId",
                table: "Asns");
        }
    }
}
