using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    [DbContext(typeof(FSH.Modules.Sourcing.Data.SourcingDbContext))]
    [Migration("20260817040000_RfqExchangeRateToBase")]
    public partial class RfqExchangeRateToBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRateToBase",
                schema: "sourcing",
                table: "Rfqs",
                type: "numeric(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRateToBase",
                schema: "sourcing",
                table: "Rfqs");
        }
    }
}
