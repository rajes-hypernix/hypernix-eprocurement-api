using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class RfqSettingsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BidValidityDays",
                schema: "sourcing",
                table: "Rfqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClarificationDeadlineUtc",
                schema: "sourcing",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncotermCode",
                schema: "sourcing",
                table: "Rfqs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncotermSuffix",
                schema: "sourcing",
                table: "Rfqs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PartialBidsAllowed",
                schema: "sourcing",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BidValidityDays",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "ClarificationDeadlineUtc",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "IncotermCode",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "IncotermSuffix",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "PartialBidsAllowed",
                schema: "sourcing",
                table: "Rfqs");
        }
    }
}
