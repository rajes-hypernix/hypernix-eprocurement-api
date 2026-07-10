using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReferenceLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceItems_Kind_Code",
                table: "ReferenceItems",
                columns: new[] { "Kind", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceItems_Kind_ParentCode",
                table: "ReferenceItems",
                columns: new[] { "Kind", "ParentCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceItems");
        }
    }
}
