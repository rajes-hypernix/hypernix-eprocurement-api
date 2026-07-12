using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D7.5 (a-parity, ruled): system views for the flat rollout mounts —
            // SystemViewSeed is THE single source; idempotent (ON CONFLICT DO NOTHING,
            // the D4 rule) so fresh-DB replays tolerate later refreshes.
            var seeded = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
            foreach (var row in Application.Views.SystemViewSeed.Rows)
            {
                var viewId = Application.Views.SystemViewSeed.ViewId(row.Code);
                migrationBuilder.Sql(SeedSql.InsertDoNothing(
                    "SavedViews",
                    ["Id", "Code", "Name", "RecordType", "OwnerUserId", "IsShared", "IsSystem", "CreatedUtc", "UpdatedUtc"],
                    [viewId, row.Code, row.Name, row.RecordType.ToString(), null, true, true, seeded, seeded]));
                for (var i = 0; i < row.Columns.Length; i++)
                {
                    migrationBuilder.Sql(SeedSql.InsertDoNothing(
                        "SavedViewColumns",
                        ["Id", "SavedViewId", "FieldKey", "Label", "Sort", "SortDirection"],
                        [Application.Views.SystemViewSeed.ColumnId(viewId, row.Columns[i]), viewId, row.Columns[i], null, i, null]));
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var row in Application.Views.SystemViewSeed.Rows)
            {
                var viewId = Application.Views.SystemViewSeed.ViewId(row.Code);
                migrationBuilder.Sql($"""DELETE FROM "SavedViewColumns" WHERE "SavedViewId" = '{viewId}';""");
                migrationBuilder.Sql($"""DELETE FROM "SavedViews" WHERE "Id" = '{viewId}';""");
            }
        }
    }
}
