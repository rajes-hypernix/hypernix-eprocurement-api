using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class PlatformSavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ViewCodeSeq",
                schema: "platform");

            migrationBuilder.CreateTable(
                name: "FieldRegistryEntries",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRegistryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewColumns",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    SortDirection = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewColumns_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalSchema: "platform",
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewFilters",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GroupIndex = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Value2 = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewFilters_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalSchema: "platform",
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldRegistryEntries_RecordType_FieldKey",
                schema: "platform",
                table: "FieldRegistryEntries",
                columns: new[] { "RecordType", "FieldKey", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewColumns_SavedViewId_Sort",
                schema: "platform",
                table: "SavedViewColumns",
                columns: new[] { "SavedViewId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewFilters_SavedViewId_Sort",
                schema: "platform",
                table: "SavedViewFilters",
                columns: new[] { "SavedViewId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_Code",
                schema: "platform",
                table: "SavedViews",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_RecordType_OwnerUserId",
                schema: "platform",
                table: "SavedViews",
                columns: new[] { "RecordType", "OwnerUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldRegistryEntries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SavedViewColumns",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SavedViewFilters",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SavedViews",
                schema: "platform");

            migrationBuilder.DropSequence(
                name: "ViewCodeSeq",
                schema: "platform");
        }
    }
}
