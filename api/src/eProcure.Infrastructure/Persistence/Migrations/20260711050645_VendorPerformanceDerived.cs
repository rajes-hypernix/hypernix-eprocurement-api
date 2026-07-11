using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VendorPerformanceDerived : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Performance_Breaches",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_Lead",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_Otd",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_Pos",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_Quality",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_Response",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_SpendYtd",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Performance_WinRate",
                table: "Vendors");

            // The stored aggregate is replaced by a derived view over the facts (AN-4/DBA-10).
            migrationBuilder.Sql(VendorPerformanceView.CreateSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Performance_Breaches",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Performance_Lead",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Performance_Otd",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Performance_Pos",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Performance_Quality",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Performance_Response",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Performance_SpendYtd",
                table: "Vendors",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Performance_WinRate",
                table: "Vendors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Restore the owned columns AND re-derive their values from the view before dropping it
            // (nulls -> 0 to fit the non-null legacy shape; Lead is re-derived in days). Not left empty.
            migrationBuilder.Sql(@"
                UPDATE ""Vendors"" v SET
                    ""Performance_Otd"" = coalesce(vpv.""Otd"", 0),
                    ""Performance_Quality"" = coalesce(vpv.""Quality"", 0),
                    ""Performance_Breaches"" = coalesce(vpv.""Breaches"", 0),
                    ""Performance_Lead"" = coalesce(vpv.""LeadDays"", 0),
                    ""Performance_Response"" = coalesce(vpv.""Response"", 0),
                    ""Performance_WinRate"" = coalesce(vpv.""WinRate"", 0),
                    ""Performance_SpendYtd"" = vpv.""SpendYtd"",
                    ""Performance_Pos"" = vpv.""Pos""
                FROM ""VendorPerformanceView"" vpv WHERE vpv.""VendorId"" = v.""Id"";");
            migrationBuilder.Sql(VendorPerformanceView.DropSql);
        }
    }
}
