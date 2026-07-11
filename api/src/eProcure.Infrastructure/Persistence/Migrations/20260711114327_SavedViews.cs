using System;
using eProcure.Application.Views;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SavedViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldRegistry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomFieldDefId = table.Column<Guid>(type: "uuid", nullable: true),
                    SegmentDefId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldRegistry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewColumns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    SortDirection = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewColumns_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedViewFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operator = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Value2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViewFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViewFilters_SavedViews_SavedViewId",
                        column: x => x.SavedViewId,
                        principalTable: "SavedViews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldRegistry_RecordType_FieldKey",
                table: "FieldRegistry",
                columns: new[] { "RecordType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewColumns_SavedViewId",
                table: "SavedViewColumns",
                column: "SavedViewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViewFilters_SavedViewId",
                table: "SavedViewFilters",
                column: "SavedViewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_Code",
                table: "SavedViews",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_RecordType_OwnerUserId",
                table: "SavedViews",
                columns: new[] { "RecordType", "OwnerUserId" });

            // ---- Registry seed (D3 Step 0(b), as ruled) — FieldRegistrySeed is THE single
            // source: this loop, the test seeders, and the reflection drift-test all read it.
            // Ids are deterministic per (RecordType, FieldKey), so re-derivation is stable.
            foreach (var row in FieldRegistrySeed.Rows)
            {
                migrationBuilder.InsertData(
                    table: "FieldRegistry",
                    columns: ["Id", "RecordType", "FieldKey", "Kind", "Label", "DataType", "CustomFieldDefId", "SegmentDefId"],
                    values: [FieldRegistrySeed.StableId(row.RecordType, row.FieldKey), row.RecordType.ToString(), row.FieldKey, "Native", row.Label, row.DataType.ToString(), null, null]);
            }

            // ---- System view seed: "All RFQs" reproduces today's default RfqList exactly
            // (columns in on-screen order, no filters) — the D3 proof screen's default.
            var viewId = new Guid("d3000000-0000-0000-0000-00000000a11f");
            var seeded = new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
            migrationBuilder.InsertData(
                table: "SavedViews",
                columns: ["Id", "Code", "Name", "RecordType", "OwnerUserId", "IsShared", "IsSystem", "CreatedUtc", "UpdatedUtc"],
                values: [viewId, "VIEW-SYS-0001", "All RFQs", "Rfq", null, true, true, seeded, seeded]);
            string[] cols = ["Code", "Title", "Envelope", "InvitedCount", "BidCount", "ClosesUtc", "Status"];
            for (var i = 0; i < cols.Length; i++)
            {
                migrationBuilder.InsertData(
                    table: "SavedViewColumns",
                    columns: ["Id", "SavedViewId", "FieldKey", "Label", "Sort", "SortDirection"],
                    values: [new Guid($"d3000000-0000-0000-0001-{i + 1:d12}"), viewId, cols[i], null, i, null]);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldRegistry");

            migrationBuilder.DropTable(
                name: "SavedViewColumns");

            migrationBuilder.DropTable(
                name: "SavedViewFilters");

            migrationBuilder.DropTable(
                name: "SavedViews");
        }
    }
}
