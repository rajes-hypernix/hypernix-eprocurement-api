using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoredFileOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerEntityId",
                table: "StoredFiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerKind",
                table: "StoredFiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Internal");   // unattributed files are Internal (buyer/admin only) — fail-closed

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerVendorId",
                table: "StoredFiles",
                type: "uuid",
                nullable: true);

            // Backfill ownership from Slice F's inference logic, so the perimeter is unchanged: bid
            // attachments -> the bidding vendor; onboarding documents / answers -> the promoted vendor;
            // anything unattributable stays Internal. The statements are shared with (and covered by)
            // StoredFileOwnershipBackfillTests — see StoredFileOwnershipBackfill.
            foreach (var sql in StoredFileOwnershipBackfill.Statements)
                migrationBuilder.Sql(sql);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_OwnerVendorId",
                table: "StoredFiles",
                column: "OwnerVendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_StoredFiles_Vendors_OwnerVendorId",
                table: "StoredFiles",
                column: "OwnerVendorId",
                principalTable: "Vendors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StoredFiles_Vendors_OwnerVendorId",
                table: "StoredFiles");

            migrationBuilder.DropIndex(
                name: "IX_StoredFiles_OwnerVendorId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "OwnerEntityId",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "OwnerKind",
                table: "StoredFiles");

            migrationBuilder.DropColumn(
                name: "OwnerVendorId",
                table: "StoredFiles");
        }
    }
}
