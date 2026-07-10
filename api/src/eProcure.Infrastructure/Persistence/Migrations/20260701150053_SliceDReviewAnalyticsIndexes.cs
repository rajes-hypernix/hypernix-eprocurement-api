using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SliceDReviewAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_InvitedByUserId",
                table: "VendorOnboardingInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingReviewSteps_DecidedByUserId",
                table: "OnboardingReviewSteps",
                column: "DecidedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorOnboardingInvitations_InvitedByUserId",
                table: "VendorOnboardingInvitations");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingReviewSteps_DecidedByUserId",
                table: "OnboardingReviewSteps");
        }
    }
}
