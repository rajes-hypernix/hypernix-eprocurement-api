using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SliceAPrLineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_PrLines",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PrLines");

            migrationBuilder.AddColumn<string>(
                name: "LineCode",
                table: "RfqLines",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CategoryCode",
                table: "PurchaseRequisitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PurchaseRequisitions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCode",
                table: "PurchaseRequisitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HeaderStatus",
                table: "PurchaseRequisitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JobCode",
                table: "PurchaseRequisitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocationCode",
                table: "PurchaseRequisitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RaisedOn",
                table: "PurchaseRequisitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequiredOn",
                table: "PurchaseRequisitions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Submitted",
                table: "PurchaseRequisitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Uom",
                table: "PrLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "PrLines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // PrLine grain key int-identity -> uuid. Postgres cannot ALTER an identity column to
            // uuid, so drop and re-add (PR lines are seed-only; no production data to preserve).
            migrationBuilder.DropColumn(
                name: "Id",
                table: "PrLines");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "PrLines",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "PrLines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedUtc",
                table: "PrLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromState",
                table: "AuditEntries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ToState",
                table: "AuditEntries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PrLines",
                table: "PrLines",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "PrLineSourcings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqLineCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    QtySourced = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LinkStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrLineSourcings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_HeaderStatus",
                table: "PurchaseRequisitions",
                column: "HeaderStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_RaisedOn",
                table: "PurchaseRequisitions",
                column: "RaisedOn");

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_ItemCode",
                table: "PrLines",
                column: "ItemCode");

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_LifecycleStatus",
                table: "PrLines",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_PurchaseRequisitionId",
                table: "PrLines",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_LinkStatus",
                table: "PrLineSourcings",
                column: "LinkStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_PrLineId",
                table: "PrLineSourcings",
                column: "PrLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_RfqId",
                table: "PrLineSourcings",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_RfqLineCode",
                table: "PrLineSourcings",
                column: "RfqLineCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrLineSourcings");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_HeaderStatus",
                table: "PurchaseRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequisitions_RaisedOn",
                table: "PurchaseRequisitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PrLines",
                table: "PrLines");

            migrationBuilder.DropIndex(
                name: "IX_PrLines_ItemCode",
                table: "PrLines");

            migrationBuilder.DropIndex(
                name: "IX_PrLines_LifecycleStatus",
                table: "PrLines");

            migrationBuilder.DropIndex(
                name: "IX_PrLines_PurchaseRequisitionId",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "LineCode",
                table: "RfqLines");

            migrationBuilder.DropColumn(
                name: "CategoryCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "DepartmentCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "HeaderStatus",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "JobCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LocationCode",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RaisedOn",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "RequiredOn",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "Submitted",
                table: "PurchaseRequisitions");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "PrLines");

            migrationBuilder.DropColumn(
                name: "FromState",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "ToState",
                table: "AuditEntries");

            migrationBuilder.AlterColumn<string>(
                name: "Uom",
                table: "PrLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ItemCode",
                table: "PrLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            // Reverse: uuid grain key -> int identity (table is empty on a reversed dev DB).
            migrationBuilder.DropColumn(
                name: "Id",
                table: "PrLines");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "PrLines",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PrLines",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PrLines",
                table: "PrLines",
                columns: new[] { "PurchaseRequisitionId", "Id" });
        }
    }
}
