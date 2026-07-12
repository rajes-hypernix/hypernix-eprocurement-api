using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntryFormLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ColumnBreak",
                table: "EntryFormFields",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "EntryFormFields",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "EntryFormSubtabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    Hidden = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormSubtabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormSubtabs_EntryFormDefs_FormDefId",
                        column: x => x.FormDefId,
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtabId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    ColumnBreak = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormGroups_EntryFormDefs_FormDefId",
                        column: x => x.FormDefId,
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntryFormGroups_EntryFormSubtabs_SubtabId",
                        column: x => x.SubtabId,
                        principalTable: "EntryFormSubtabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormFields_GroupId",
                table: "EntryFormFields",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormGroups_FormDefId_SubtabId_Title",
                table: "EntryFormGroups",
                columns: new[] { "FormDefId", "SubtabId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormGroups_SubtabId",
                table: "EntryFormGroups",
                column: "SubtabId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormSubtabs_FormDefId_Name",
                table: "EntryFormSubtabs",
                columns: new[] { "FormDefId", "Name" },
                unique: true);

            // ---- CF5 BACKFILL: string placement → objects, with the SAME md5 id derivations
            // EntryFormSeed uses in C# (md5(text)::uuid == HexGuid — the SegmentSeed rule), so a
            // fresh-DB replay (prior migration seeds strings, this one materializes objects) and
            // an upgraded live DB both land on IDENTICAL deterministic ids. Sort = order of first
            // appearance by field Sort. Runs before the FK so every GroupId resolves. ----
            migrationBuilder.Sql("""
                INSERT INTO "EntryFormSubtabs" ("Id", "FormDefId", "Name", "Sort", "Hidden")
                SELECT md5('entryformsubtab:' || s."FormDefId"::text || ':' || s."Subtab")::uuid,
                       s."FormDefId", s."Subtab",
                       (row_number() OVER (PARTITION BY s."FormDefId" ORDER BY s."MinSort"))::int - 1,
                       false
                FROM (SELECT "FormDefId", "Subtab", min("Sort") AS "MinSort"
                      FROM "EntryFormFields" WHERE "Subtab" IS NOT NULL
                      GROUP BY "FormDefId", "Subtab") s
                ON CONFLICT DO NOTHING;
                """);
            migrationBuilder.Sql("""
                INSERT INTO "EntryFormGroups" ("Id", "FormDefId", "SubtabId", "Title", "Sort", "ColumnBreak")
                SELECT md5('entryformgroup:' || g."FormDefId"::text || ':' || coalesce(g."Subtab", '') || ':' || g."FieldGroup")::uuid,
                       g."FormDefId",
                       CASE WHEN g."Subtab" IS NULL THEN NULL
                            ELSE md5('entryformsubtab:' || g."FormDefId"::text || ':' || g."Subtab")::uuid END,
                       g."FieldGroup",
                       (row_number() OVER (PARTITION BY g."FormDefId" ORDER BY g."MinSort"))::int - 1,
                       false
                FROM (SELECT "FormDefId", "Subtab", "FieldGroup", min("Sort") AS "MinSort"
                      FROM "EntryFormFields"
                      GROUP BY "FormDefId", "Subtab", "FieldGroup") g
                ON CONFLICT DO NOTHING;
                """);
            migrationBuilder.Sql("""
                UPDATE "EntryFormFields" f
                SET "GroupId" = md5('entryformgroup:' || f."FormDefId"::text || ':' || coalesce(f."Subtab", '') || ':' || f."FieldGroup")::uuid;
                """);

            migrationBuilder.DropColumn(
                name: "FieldGroup",
                table: "EntryFormFields");

            migrationBuilder.DropColumn(
                name: "Subtab",
                table: "EntryFormFields");

            migrationBuilder.AddForeignKey(
                name: "FK_EntryFormFields_EntryFormGroups_GroupId",
                table: "EntryFormFields",
                column: "GroupId",
                principalTable: "EntryFormGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntryFormFields_EntryFormGroups_GroupId",
                table: "EntryFormFields");

            migrationBuilder.AddColumn<string>(name: "FieldGroup", table: "EntryFormFields",
                type: "character varying(80)", maxLength: 80, nullable: false, defaultValue: "Header");
            migrationBuilder.AddColumn<string>(name: "Subtab", table: "EntryFormFields",
                type: "character varying(80)", maxLength: 80, nullable: true);
            migrationBuilder.Sql("""
                UPDATE "EntryFormFields" f
                SET "FieldGroup" = g."Title",
                    "Subtab" = s."Name"
                FROM "EntryFormGroups" g
                LEFT JOIN "EntryFormSubtabs" s ON s."Id" = g."SubtabId"
                WHERE g."Id" = f."GroupId";
                """);

            migrationBuilder.DropTable(
                name: "EntryFormGroups");

            migrationBuilder.DropTable(
                name: "EntryFormSubtabs");

            migrationBuilder.DropIndex(
                name: "IX_EntryFormFields_GroupId",
                table: "EntryFormFields");

            migrationBuilder.DropColumn(
                name: "ColumnBreak",
                table: "EntryFormFields");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "EntryFormFields");

            migrationBuilder.AddColumn<string>(
                name: "FieldGroup",
                table: "EntryFormFields",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subtab",
                table: "EntryFormFields",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }
    }
}
