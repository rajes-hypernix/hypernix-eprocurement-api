using System;
using eProcure.Application.Dashboards;
using eProcure.Domain.Identity;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// D4 Phase 3: appends the remaining portlet types (Reminders, Shortcuts, RecentRecords —
    /// buyer-default indexes 6–8) so every type ships LIVE. Earlier seeded portlets keep their
    /// deterministic ids; this migration inserts only the appended rows.
    /// </summary>
    public partial class DashboardSeedRefresh : Migration
    {
        private static readonly string BuyerRole = Roles.Buyer;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var buyer = DashboardSeed.Rows.Single(r => r.OwnerRole == BuyerRole);
            for (var i = 6; i < buyer.Portlets.Count; i++)
            {
                var p = buyer.Portlets[i];
                migrationBuilder.InsertData(
                    table: "PortletInstances",
                    columns: ["Id", "DashboardId", "PortletType", "Title", "Col", "Row", "Width", "SavedViewId", "ConfigJson"],
                    values: [DashboardSeed.PortletId(BuyerRole, i), DashboardSeed.DashboardId(BuyerRole), p.Type.ToString(), p.Title, p.Col, p.Row, p.Width, p.SavedViewId, p.ConfigJson]);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            for (var i = 6; i <= 8; i++)
                migrationBuilder.DeleteData(
                    table: "PortletInstances", keyColumn: "Id",
                    keyValue: DashboardSeed.PortletId(BuyerRole, i));
        }
    }
}
