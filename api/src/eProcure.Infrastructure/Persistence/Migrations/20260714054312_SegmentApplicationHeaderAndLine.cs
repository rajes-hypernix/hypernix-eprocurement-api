using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SegmentApplicationHeaderAndLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SegmentApplications_SegmentDefId_RecordType",
                table: "SegmentApplications");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentApplications_SegmentDefId_RecordType_LineLevel",
                table: "SegmentApplications",
                columns: new[] { "SegmentDefId", "RecordType", "LineLevel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SegmentApplications_SegmentDefId_RecordType_LineLevel",
                table: "SegmentApplications");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentApplications_SegmentDefId_RecordType",
                table: "SegmentApplications",
                columns: new[] { "SegmentDefId", "RecordType" },
                unique: true);
        }
    }
}
