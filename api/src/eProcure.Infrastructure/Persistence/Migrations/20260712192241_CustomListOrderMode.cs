using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomListOrderMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CF-FIX2 REPAIR: was defaultValue: false — which silently DEACTIVATED every
            // pre-existing list on upgrade (found by the CF-FIX2-T2 browser proof: vendor-form
            // pickers were empty). Lists are born active; the entity default is true.
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "CustomLists",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderMode",
                table: "CustomLists",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Active",
                table: "CustomLists");

            migrationBuilder.DropColumn(
                name: "OrderMode",
                table: "CustomLists");
        }
    }
}
