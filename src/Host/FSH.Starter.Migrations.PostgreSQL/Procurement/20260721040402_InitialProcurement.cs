using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Procurement
{
    /// <inheritdoc />
    public partial class InitialProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "procurement");

            migrationBuilder.CreateSequence(
                name: "AsnCodeSeq",
                schema: "procurement");

            migrationBuilder.CreateSequence(
                name: "GrnCodeSeq",
                schema: "procurement");

            migrationBuilder.CreateSequence(
                name: "InvCodeSeq",
                schema: "procurement");

            migrationBuilder.CreateSequence(
                name: "PoCodeSeq",
                schema: "procurement");

            migrationBuilder.CreateTable(
                name: "Asns",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Carrier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TrackingNo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShippedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grns",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AsnId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrnId = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WhtRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExceptionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AwardId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AsnLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AsnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ShippedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LotNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsnLines_Asns_AsnId",
                        column: x => x.AsnId,
                        principalSchema: "procurement",
                        principalTable: "Asns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrnLines_Grns_GrnId",
                        column: x => x.GrnId,
                        principalSchema: "procurement",
                        principalTable: "Grns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "procurement",
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InvoicedQty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RfqLineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsnLines_AsnId",
                schema: "procurement",
                table: "AsnLines",
                column: "AsnId");

            migrationBuilder.CreateIndex(
                name: "IX_Asns_Code",
                schema: "procurement",
                table: "Asns",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrnLines_GrnId",
                schema: "procurement",
                table: "GrnLines",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_AsnId",
                schema: "procurement",
                table: "Grns",
                columns: new[] { "AsnId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Code",
                schema: "procurement",
                table: "Grns",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                schema: "procurement",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Code",
                schema: "procurement",
                table: "Invoices",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoLines_PurchaseOrderId",
                schema: "procurement",
                table: "PoLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Code",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "Code", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsnLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "GrnLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "InvoiceLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PoLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Asns",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Grns",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "procurement");

            migrationBuilder.DropSequence(
                name: "AsnCodeSeq",
                schema: "procurement");

            migrationBuilder.DropSequence(
                name: "GrnCodeSeq",
                schema: "procurement");

            migrationBuilder.DropSequence(
                name: "InvCodeSeq",
                schema: "procurement");

            migrationBuilder.DropSequence(
                name: "PoCodeSeq",
                schema: "procurement");
        }
    }
}
