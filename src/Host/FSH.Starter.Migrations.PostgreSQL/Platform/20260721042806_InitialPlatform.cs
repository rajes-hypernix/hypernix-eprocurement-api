using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class InitialPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "Banks",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SwiftCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomLists",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "States",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.Id);
                    table.ForeignKey(
                        name: "FK_States_Countries_CountryId",
                        column: x => x.CountryId,
                        principalSchema: "platform",
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomListItems",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomListItems_CustomLists_ListId",
                        column: x => x.ListId,
                        principalSchema: "platform",
                        principalTable: "CustomLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cities",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cities_States_StateId",
                        column: x => x.StateId,
                        principalSchema: "platform",
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Banks_CountryCode_Name",
                schema: "platform",
                table: "Banks",
                columns: new[] { "CountryCode", "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cities_StateId_Name",
                schema: "platform",
                table: "Cities",
                columns: new[] { "StateId", "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Countries_Code",
                schema: "platform",
                table: "Countries",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomListItems_ListId_Code",
                schema: "platform",
                table: "CustomListItems",
                columns: new[] { "ListId", "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomLists_Key",
                schema: "platform",
                table: "CustomLists",
                columns: new[] { "Key", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_States_CountryId_Code",
                schema: "platform",
                table: "States",
                columns: new[] { "CountryId", "Code", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Banks",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Cities",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CustomListItems",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "States",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CustomLists",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "platform");
        }
    }
}
