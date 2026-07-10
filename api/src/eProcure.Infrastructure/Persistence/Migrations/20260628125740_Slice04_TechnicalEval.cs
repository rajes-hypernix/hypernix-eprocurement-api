using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice04_TechnicalEval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TechnicalOpened",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TechnicalScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatorId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Criterion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalScores_RfqId_VendorId_EvaluatorId_Criterion",
                table: "TechnicalScores",
                columns: new[] { "RfqId", "VendorId", "EvaluatorId", "Criterion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalScores");

            migrationBuilder.DropColumn(
                name: "TechnicalOpened",
                table: "Rfqs");
        }
    }
}
