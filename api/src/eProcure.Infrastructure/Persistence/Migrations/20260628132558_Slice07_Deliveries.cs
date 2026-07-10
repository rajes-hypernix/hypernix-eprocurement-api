using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice07_Deliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Asns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Carrier = table.Column<string>(type: "text", nullable: false),
                    TrackingNo = table.Column<string>(type: "text", nullable: false),
                    ShippedDate = table.Column<string>(type: "text", nullable: false),
                    ExpectedDate = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GrnCode = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AsnId = table.Column<Guid>(type: "uuid", nullable: false),
                    PoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedDate = table.Column<string>(type: "text", nullable: false),
                    ReceivedBy = table.Column<string>(type: "text", nullable: false),
                    NsId = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AsnLines",
                columns: table => new
                {
                    AsnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ShippedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Uom = table.Column<string>(type: "text", nullable: false),
                    LotNo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsnLines", x => new { x.AsnId, x.Id });
                    table.ForeignKey(
                        name: "FK_AsnLines_Asns_AsnId",
                        column: x => x.AsnId,
                        principalTable: "Asns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrnLines",
                columns: table => new
                {
                    GrnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemCode = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ExpectedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrnLines", x => new { x.GrnId, x.Id });
                    table.ForeignKey(
                        name: "FK_GrnLines_Grns_GrnId",
                        column: x => x.GrnId,
                        principalTable: "Grns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asns_Code",
                table: "Asns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grns_Code",
                table: "Grns",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsnLines");

            migrationBuilder.DropTable(
                name: "GrnLines");

            migrationBuilder.DropTable(
                name: "Asns");

            migrationBuilder.DropTable(
                name: "Grns");
        }
    }
}
