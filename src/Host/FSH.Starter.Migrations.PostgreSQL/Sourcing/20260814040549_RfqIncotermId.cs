using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class RfqIncotermId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncotermId",
                schema: "sourcing",
                table: "Rfqs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_IncotermId",
                schema: "sourcing",
                table: "Rfqs",
                column: "IncotermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rfqs_IncotermId",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "IncotermId",
                schema: "sourcing",
                table: "Rfqs");
        }
    }
}
