using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Slice11OwnersRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Rfqs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientUserId",
                table: "Clarifications",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "Clarifications");
        }
    }
}
