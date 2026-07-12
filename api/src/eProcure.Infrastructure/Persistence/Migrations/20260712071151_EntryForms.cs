using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntryForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryFormDefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormDefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberingSchemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    YearSegment = table.Column<bool>(type: "boolean", nullable: false),
                    Digits = table.Column<int>(type: "integer", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Subtab = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    FieldGroup = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    DisplayType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredOnForm = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    SourceFieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FullWidth = table.Column<bool>(type: "boolean", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Placeholder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormFields_EntryFormDefs_FormDefId",
                        column: x => x.FormDefId,
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormRoleMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FormDefId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormRoleMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormRoleMaps_EntryFormDefs_FormDefId",
                        column: x => x.FormDefId,
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormDefs_Code",
                table: "EntryFormDefs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormFields_FormDefId_FieldKey",
                table: "EntryFormFields",
                columns: new[] { "FormDefId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormRoleMaps_FormDefId",
                table: "EntryFormRoleMaps",
                column: "FormDefId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormRoleMaps_RecordType_Role",
                table: "EntryFormRoleMaps",
                columns: new[] { "RecordType", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSchemes_RecordType",
                table: "NumberingSchemes",
                column: "RecordType",
                unique: true);

            // ---- D7 seeds — EntryFormSeed is THE single source (migration + tests + the
            // parity probe). Idempotent (ON CONFLICT DO NOTHING, the D4 rule): a fresh-DB
            // replay must tolerate rows a later slice's refresh may have inserted first. ----
            var seeded = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
            var formId = Application.Forms.EntryFormSeed.FormId(Application.Forms.EntryFormSeed.StandardPrFormCode);
            migrationBuilder.Sql(SeedSql.InsertDoNothing(
                "EntryFormDefs",
                ["Id", "Code", "Name", "RecordType", "IsSystem", "Active", "CreatedUtc", "UpdatedUtc"],
                [formId, Application.Forms.EntryFormSeed.StandardPrFormCode,
                 Application.Forms.EntryFormSeed.StandardPrFormName, "Requisition", true, true, seeded, seeded]));
            foreach (var f in Application.Forms.EntryFormSeed.StandardPrFields)
            {
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "EntryFormFields",
                    ["Id", "FormDefId", "FieldKey", "Subtab", "FieldGroup", "Sort", "DisplayType",
                     "RequiredOnForm", "DefaultValue", "SourceFieldKey", "FullWidth", "Label", "Placeholder"],
                    [Application.Forms.EntryFormSeed.FieldId(formId, f.FieldKey), formId, f.FieldKey, f.Subtab,
                     f.FieldGroup, f.Sort, "Normal", false, null, null, f.FullWidth, f.Label, f.Placeholder]));
            }
            foreach (var s in Application.Forms.EntryFormSeed.Schemes)
            {
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "NumberingSchemes",
                    ["Id", "RecordType", "Prefix", "YearSegment", "Digits", "UpdatedUtc"],
                    [Application.Forms.EntryFormSeed.SchemeId(s.RecordType), s.RecordType.ToString(),
                     s.Prefix, s.YearSegment, s.Digits, seeded]));
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryFormFields");

            migrationBuilder.DropTable(
                name: "EntryFormRoleMaps");

            migrationBuilder.DropTable(
                name: "NumberingSchemes");

            migrationBuilder.DropTable(
                name: "EntryFormDefs");
        }
    }
}
