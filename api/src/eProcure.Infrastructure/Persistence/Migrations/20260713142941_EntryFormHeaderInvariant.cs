using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntryFormHeaderInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHeader",
                table: "EntryFormGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // CF-FIX4-T1 backfill (L3, the Header invariant):
            // 1. Every BODY group titled 'Header' becomes THE header group of its form.
            // 2. Any form left without one gets a Header group created -- md5-deterministic id,
            //    the SAME derivation as EntryFormSeed.GroupId / the CF5 EntryFormLayout backfill
            //    (md5(text)::uuid == HexGuid, the SegmentSeed rule), Sort=-1 so it renders first.
            migrationBuilder.Sql("""
                UPDATE "EntryFormGroups" SET "IsHeader" = TRUE
                WHERE "SubtabId" IS NULL AND "Title" = 'Header';

                INSERT INTO "EntryFormGroups" ("Id", "FormDefId", "SubtabId", "Title", "Sort", "ColumnBreak", "IsHeader")
                SELECT md5('entryformgroup:' || d."Id"::text || '::Header')::uuid,
                       d."Id", NULL, 'Header', -1, FALSE, TRUE
                FROM "EntryFormDefs" d
                WHERE NOT EXISTS (
                    SELECT 1 FROM "EntryFormGroups" g WHERE g."FormDefId" = d."Id" AND g."IsHeader")
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHeader",
                table: "EntryFormGroups");
        }
    }
}
