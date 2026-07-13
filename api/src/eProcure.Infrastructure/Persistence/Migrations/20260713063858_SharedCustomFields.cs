using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.CreateTable(
                name: "CustomFieldDefApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefApplications_CustomFieldDefs_FieldDefId",
                        column: x => x.FieldDefId,
                        principalTable: "CustomFieldDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefApplications_FieldDefId_RecordType",
                table: "CustomFieldDefApplications",
                columns: new[] { "FieldDefId", "RecordType" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefs_RecordType_Active",
                table: "CustomFieldDefs");

            // CF-FIX2-T3 BACKFILL (the confirmed no-auto-merge strategy): every existing def
            // becomes a shared-capable def applied to EXACTLY its current record type —
            // behaviour identical to before; same-named duplicates are NOT merged (cannot
            // prove semantic identity; consolidation is a manual admin action).
            migrationBuilder.Sql("""
                INSERT INTO "CustomFieldDefApplications" ("Id", "FieldDefId", "RecordType")
                SELECT md5('cfapp:' || "Id"::text || ':' || "RecordType")::uuid, "Id", "RecordType"
                FROM "CustomFieldDefs";
                """);

            migrationBuilder.DropColumn(
                name: "RecordType",
                table: "CustomFieldDefs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldDefApplications");

            migrationBuilder.AddColumn<string>(
                name: "RecordType",
                table: "CustomFieldDefs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_RecordType_Active",
                table: "CustomFieldDefs",
                columns: new[] { "RecordType", "Active" });
        }
    }
}
