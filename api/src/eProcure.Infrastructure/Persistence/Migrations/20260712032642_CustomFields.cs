using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomFieldDefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomListId = table.Column<Guid>(type: "uuid", nullable: true),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    HelpText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefs_CustomLists_CustomListId",
                        column: x => x.CustomListId,
                        principalTable: "CustomLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValueText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ValueMoney = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValueBool = table.Column<bool>(type: "boolean", nullable: true),
                    ValueListCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                    table.CheckConstraint("CK_CustomFieldValues_ExactlyOne", "(CASE WHEN \"ValueText\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueNumber\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueMoney\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueDate\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueBool\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueListCode\" IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("CK_CustomFieldValues_KindMatch", "(\"DataType\" IN ('Text','LongText') AND \"ValueText\" IS NOT NULL) OR (\"DataType\" IN ('Int','Decimal') AND \"ValueNumber\" IS NOT NULL) OR (\"DataType\" = 'Money' AND \"ValueMoney\" IS NOT NULL) OR (\"DataType\" = 'Date' AND \"ValueDate\" IS NOT NULL) OR (\"DataType\" = 'Bool' AND \"ValueBool\" IS NOT NULL) OR (\"DataType\" = 'ListValue' AND \"ValueListCode\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CustomFieldValues_CustomFieldDefs_FieldDefId",
                        column: x => x.FieldDefId,
                        principalTable: "CustomFieldDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_Code",
                table: "CustomFieldDefs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_CustomListId",
                table: "CustomFieldDefs",
                column: "CustomListId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_RecordType_Active",
                table: "CustomFieldDefs",
                columns: new[] { "RecordType", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldDefId_RecordId",
                table: "CustomFieldValues",
                columns: new[] { "FieldDefId", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_RecordType_RecordId",
                table: "CustomFieldValues",
                columns: new[] { "RecordType", "RecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldValues");

            migrationBuilder.DropTable(
                name: "CustomFieldDefs");
        }
    }
}
