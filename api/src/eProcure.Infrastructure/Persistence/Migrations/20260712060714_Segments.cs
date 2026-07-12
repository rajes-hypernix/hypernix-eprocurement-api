using System;
using eProcure.Application.Segments;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Segments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SegmentDefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HasHierarchy = table.Column<bool>(type: "boolean", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentDefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SegmentApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LineLevel = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentApplications_SegmentDefs_SegmentDefId",
                        column: x => x.SegmentDefId,
                        principalTable: "SegmentDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ParentValueId = table.Column<Guid>(type: "uuid", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentValues_SegmentDefs_SegmentDefId",
                        column: x => x.SegmentDefId,
                        principalTable: "SegmentDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SegmentValues_SegmentValues_ParentValueId",
                        column: x => x.ParentValueId,
                        principalTable: "SegmentValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    SegmentValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SegmentAssignments_SegmentDefs_SegmentDefId",
                        column: x => x.SegmentDefId,
                        principalTable: "SegmentDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SegmentAssignments_SegmentValues_SegmentValueId",
                        column: x => x.SegmentValueId,
                        principalTable: "SegmentValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SegmentApplications_SegmentDefId_RecordType",
                table: "SegmentApplications",
                columns: new[] { "SegmentDefId", "RecordType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentAssignments_RecordType_RecordId",
                table: "SegmentAssignments",
                columns: new[] { "RecordType", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_SegmentAssignments_SegmentDefId_RecordType_RecordId_LineId",
                table: "SegmentAssignments",
                columns: new[] { "SegmentDefId", "RecordType", "RecordId", "LineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentAssignments_SegmentValueId",
                table: "SegmentAssignments",
                column: "SegmentValueId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentDefs_Code",
                table: "SegmentDefs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentValues_ParentValueId",
                table: "SegmentValues",
                column: "ParentValueId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentValues_SegmentDefId_Code",
                table: "SegmentValues",
                columns: new[] { "SegmentDefId", "Code" },
                unique: true);

            // ---- T6 system segments (ruled (iii-a)): the four PR dimensions seeded as system
            // SegmentDefs, VALUES distilled from the existing *Code/label COLUMNS (code identity
            // with SourcingMapping.DimCode holds BY CONSTRUCTION — the columns store DimCode
            // output), ASSIGNMENTS backfilled per PR. Idempotent (standing procedure); all ids
            // deterministic (md5-hex — SegmentSeed uses the same straight-hex derivation).
            var seeded = "TIMESTAMPTZ '2026-07-12 00:00:00Z'";
            foreach (var s in SegmentSeed.SystemSegments)
            {
                var defId = SegmentSeed.DefId(s.Code);
                migrationBuilder.Sql(
                    $"INSERT INTO \"SegmentDefs\" (\"Id\",\"Code\",\"Name\",\"HasHierarchy\",\"Required\",\"Active\",\"IsSystem\",\"CreatedUtc\",\"UpdatedUtc\") " +
                    $"VALUES ('{defId}','{s.Code}','{s.Name}',FALSE,FALSE,TRUE,TRUE,{seeded},{seeded}) ON CONFLICT (\"Id\") DO NOTHING;");
                migrationBuilder.Sql(
                    $"INSERT INTO \"SegmentApplications\" (\"Id\",\"SegmentDefId\",\"RecordType\",\"LineLevel\") " +
                    $"VALUES (md5('segapp:{defId}:Requisition')::uuid,'{defId}','Requisition',FALSE) ON CONFLICT (\"Id\") DO NOTHING;");
                migrationBuilder.Sql(
                    $"INSERT INTO \"SegmentValues\" (\"Id\",\"SegmentDefId\",\"Code\",\"Label\",\"ParentValueId\",\"Active\",\"Sort\") " +
                    $"SELECT DISTINCT ON (d.code) md5('segval:{defId}:' || d.code)::uuid, '{defId}', d.code, d.label, NULL, TRUE, 0 " +
                    $"FROM (SELECT \"{s.ColumnCode}\" AS code, \"{s.ColumnLabel}\" AS label FROM \"PurchaseRequisitions\" WHERE \"{s.ColumnCode}\" <> '') d " +
                    $"ON CONFLICT (\"Id\") DO NOTHING;");
                migrationBuilder.Sql(
                    $"INSERT INTO \"SegmentAssignments\" (\"Id\",\"SegmentDefId\",\"SegmentValueId\",\"RecordType\",\"RecordId\",\"LineId\",\"UpdatedUtc\") " +
                    $"SELECT md5('segassign:{defId}:' || p.\"Id\")::uuid, '{defId}', md5('segval:{defId}:' || p.\"{s.ColumnCode}\")::uuid, 'Requisition', p.\"Id\", NULL, {seeded} " +
                    $"FROM \"PurchaseRequisitions\" p WHERE p.\"{s.ColumnCode}\" <> '' " +
                    $"ON CONFLICT (\"Id\") DO NOTHING;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SegmentApplications");

            migrationBuilder.DropTable(
                name: "SegmentAssignments");

            migrationBuilder.DropTable(
                name: "SegmentValues");

            migrationBuilder.DropTable(
                name: "SegmentDefs");
        }
    }
}
