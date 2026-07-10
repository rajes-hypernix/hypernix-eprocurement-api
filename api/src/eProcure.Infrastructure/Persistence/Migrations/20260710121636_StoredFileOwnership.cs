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
            // attachments (answer-value "<fileId>::name" convention) -> the bidding vendor; onboarding
            // documents / answers -> the promoted vendor. First match wins (Bid > Document > Answer);
            // anything unattributable stays Internal. Valid GUID-prefixed entries only.
            migrationBuilder.Sql(@"
                UPDATE ""StoredFiles"" sf
                SET ""OwnerKind"" = 'Bid', ""OwnerVendorId"" = b.""VendorId"", ""OwnerEntityId"" = b.""Id""
                FROM ""Bids"" b
                JOIN ""BidAnswers"" ba ON ba.""BidId"" = b.""Id""
                CROSS JOIN LATERAL regexp_split_to_table(ba.""Value"", '\|') AS entry
                WHERE entry ~ '^[0-9a-fA-F-]{36}::'
                  AND sf.""Id"" = substring(entry from 1 for 36)::uuid;");

            migrationBuilder.Sql(@"
                UPDATE ""StoredFiles"" sf
                SET ""OwnerKind"" = 'OnboardingDocument', ""OwnerVendorId"" = a.""PromotedVendorId"", ""OwnerEntityId"" = a.""Id""
                FROM ""OnboardingDocuments"" od
                JOIN ""VendorOnboardingApplications"" a ON a.""Id"" = od.""VendorOnboardingApplicationId""
                WHERE sf.""Id"" = od.""StoredFileId"" AND sf.""OwnerKind"" = 'Internal';");

            migrationBuilder.Sql(@"
                UPDATE ""StoredFiles"" sf
                SET ""OwnerKind"" = 'OnboardingAnswer', ""OwnerVendorId"" = a.""PromotedVendorId"", ""OwnerEntityId"" = a.""Id""
                FROM ""VendorOnboardingApplications"" a
                JOIN ""OnboardingAnswers"" oa ON oa.""VendorOnboardingApplicationId"" = a.""Id""
                CROSS JOIN LATERAL regexp_split_to_table(oa.""Value"", '\|') AS entry
                WHERE entry ~ '^[0-9a-fA-F-]{36}::'
                  AND sf.""Id"" = substring(entry from 1 for 36)::uuid
                  AND sf.""OwnerKind"" = 'Internal';");

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
