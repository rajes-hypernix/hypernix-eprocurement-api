using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuickWinsHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                table: "VendorOnboardingInvitations");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "Awards");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedUtc",
                table: "TechnicalScores",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill existing scores: the last-updated stamp is the best available proxy for when
            // the score was first created — no row is left with the 0001-01-01 sentinel.
            migrationBuilder.Sql(@"UPDATE ""TechnicalScores"" SET ""CreatedUtc"" = ""UpdatedUtc"";");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                table: "VendorOnboardingInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Awards_RfqId",
                table: "Awards",
                column: "RfqId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                table: "VendorOnboardingInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Awards_RfqId",
                table: "Awards");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "TechnicalScores");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "Awards",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // Restore the dropped value from the allocations it was always derived from — TotalValue
            // is fully reconstructable, so Down() must not leave zeros (same discipline as the
            // Slice I string_agg reconstruction).
            migrationBuilder.Sql(@"
                UPDATE ""Awards"" a
                SET ""TotalValue"" = COALESCE((
                    SELECT SUM(al.""Qty"" * al.""UnitPrice"")
                    FROM ""AwardAllocations"" al
                    WHERE al.""AwardId"" = a.""Id""
                ), 0);");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                table: "VendorOnboardingInvitations",
                column: "TokenHash");
        }
    }
}
