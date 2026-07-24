using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class RequisitionOrderingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrLineOrders",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoLineRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    QtyOrdered = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LinkStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrLineOrders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrLineOrders_LinkStatus",
                schema: "sourcing",
                table: "PrLineOrders",
                column: "LinkStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineOrders_PoId",
                schema: "sourcing",
                table: "PrLineOrders",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineOrders_PrLineId",
                schema: "sourcing",
                table: "PrLineOrders",
                column: "PrLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrLineOrders",
                schema: "sourcing");
        }
    }
}
