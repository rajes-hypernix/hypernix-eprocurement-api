using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceItems");

            migrationBuilder.CreateTable(
                name: "CustomLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ParentListCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomListValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentValueCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomListValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomListValues_CustomLists_CustomListId",
                        column: x => x.CustomListId,
                        principalTable: "CustomLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomLists_Code",
                table: "CustomLists",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomListValues_CustomListId",
                table: "CustomListValues",
                column: "CustomListId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomListValues_CustomListId_Code",
                table: "CustomListValues",
                columns: new[] { "CustomListId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomListValues_CustomListId_ParentValueCode",
                table: "CustomListValues",
                columns: new[] { "CustomListId", "ParentValueCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomListValues");

            migrationBuilder.DropTable(
                name: "CustomLists");

            migrationBuilder.CreateTable(
                name: "ReferenceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
    }
}
