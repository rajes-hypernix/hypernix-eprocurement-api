using System;
using Microsoft.EntityFrameworkCore.Migrations;
using eProcure.Infrastructure.Persistence;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntryFormSublistColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntryFormSublistColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormSublistColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormSublistColumns_EntryFormDefs_FormDefId",
                        column: x => x.FormDefId,
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormSublistColumns_FormDefId_FieldKey",
                table: "EntryFormSublistColumns",
                columns: new[] { "FormDefId", "FieldKey" },
                unique: true);

            // CF-FIX4-T3 seed: the standard PR form's sublist order = PrForm's hardcoded
            // lines table, verbatim (parity — rendering from rows equals the markup).
            var prFormId = Application.Forms.EntryFormSeed.FormId(Application.Forms.EntryFormSeed.StandardPrFormCode);
            var sort = 0;
            foreach (var key in Application.Forms.EntryFormSeed.StandardPrSublistColumns)
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "EntryFormSublistColumns",
                    ["Id", "FormDefId", "FieldKey", "Sort"],
                    [Application.Forms.EntryFormSeed.SublistColumnId(prFormId, key), prFormId, key, sort++]));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntryFormSublistColumns");
        }
    }
}
