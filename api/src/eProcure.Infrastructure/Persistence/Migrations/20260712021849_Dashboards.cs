using System;
using eProcure.Application.Dashboards;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Dashboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dashboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OwnerUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRoleDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dashboards", x => x.Id);
                    table.CheckConstraint("CK_Dashboards_OneOwnerAxis", "(\"OwnerRole\" IS NOT NULL AND \"OwnerUserId\" IS NULL AND \"IsRoleDefault\") OR (\"OwnerRole\" IS NULL AND \"OwnerUserId\" IS NOT NULL AND NOT \"IsRoleDefault\")");
                });

            migrationBuilder.CreateTable(
                name: "PortletInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DashboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortletType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Col = table.Column<short>(type: "smallint", nullable: false),
                    Row = table.Column<short>(type: "smallint", nullable: false),
                    Width = table.Column<short>(type: "smallint", nullable: false),
                    SavedViewId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortletInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortletInstances_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_Code",
                table: "Dashboards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_OwnerUserId",
                table: "Dashboards",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortletInstances_DashboardId",
                table: "PortletInstances",
                column: "DashboardId");

            var seeded = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);

            // The shared "Recent purchase orders" system view the SavedViewList portlet rides —
            // the honest replacement for the mock RECENT_POS table (top-N by Code desc).
            migrationBuilder.InsertData(
                table: "SavedViews",
                columns: ["Id", "Code", "Name", "RecordType", "OwnerUserId", "IsShared", "IsSystem", "CreatedUtc", "UpdatedUtc"],
                values: [DashboardSeed.RecentPosViewId, "VIEW-SYS-0002", "Recent purchase orders", "PurchaseOrder", null, true, true, seeded, seeded]);
            string[] poCols = ["Code", "VendorName", "RfqCode", "Total", "Status"];
            for (var i = 0; i < poCols.Length; i++)
            {
                migrationBuilder.InsertData(
                    table: "SavedViewColumns",
                    columns: ["Id", "SavedViewId", "FieldKey", "Label", "Sort", "SortDirection"],
                    values: [new Guid($"d4000000-0000-0000-0001-{i + 1:d12}"), DashboardSeed.RecentPosViewId, poCols[i], null, i, i == 0 ? "Desc" : null]);
            }

            // Role-default dashboards (D4 Step 0(a)/(d), ruled) — DashboardSeed is THE single
            // source shared with tests; ids deterministic per (role, index).
            // Idempotent (ON CONFLICT DO NOTHING): DashboardSeed.Rows GROWS in later slices
            // (DashboardSeedRefresh appended buyer portlets) — a fresh-DB replay of this
            // migration inserts the grown list and the refresh must then no-op, not collide.
            foreach (var row in DashboardSeed.Rows)
            {
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "Dashboards",
                    ["Id", "Code", "Name", "OwnerRole", "OwnerUserId", "IsRoleDefault", "CreatedUtc", "UpdatedUtc"],
                    [DashboardSeed.DashboardId(row.OwnerRole), row.Code, row.Name, row.OwnerRole, null, true, seeded, seeded]));
                for (var i = 0; i < row.Portlets.Count; i++)
                {
                    var p = row.Portlets[i];
                    migrationBuilder.Sql(SeedSql.InsertDoNothing(
                        "PortletInstances",
                        ["Id", "DashboardId", "PortletType", "Title", "Col", "Row", "Width", "SavedViewId", "ConfigJson"],
                        [DashboardSeed.PortletId(row.OwnerRole, i), DashboardSeed.DashboardId(row.OwnerRole), p.Type.ToString(), p.Title, p.Col, p.Row, p.Width, p.SavedViewId, p.ConfigJson]));
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortletInstances");

            migrationBuilder.DropTable(
                name: "Dashboards");

            // The seeded system view lives in D3's tables (not dropped here) — remove it so
            // Down leaves no trace.
            for (var i = 0; i < 5; i++)
                migrationBuilder.DeleteData(
                    table: "SavedViewColumns", keyColumn: "Id",
                    keyValue: new Guid($"d4000000-0000-0000-0001-{i + 1:d12}"));
            migrationBuilder.DeleteData(
                table: "SavedViews", keyColumn: "Id", keyValue: DashboardSeed.RecentPosViewId);
        }
    }
}
