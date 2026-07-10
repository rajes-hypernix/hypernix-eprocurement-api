using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RfqInvitationsAndEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) New columns (RoundNumber defaults to 1 — the entity default; §2.3).
            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalClosesUtc",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoundNumber",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnUtc",
                table: "Bids",
                type: "timestamp with time zone",
                nullable: true);

            // 2) New tables + indexes.
            migrationBuilder.CreateTable(
                name: "RfqEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorVendorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReasonNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OldClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqEvents_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RfqInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclineReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DeclineNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RescindReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RescindNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InvitedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RescindedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqInvitations_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfqInvitations_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RfqEvents_EventType_OccurredUtc",
                table: "RfqEvents",
                columns: new[] { "EventType", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqEvents_RfqId_OccurredUtc",
                table: "RfqEvents",
                columns: new[] { "RfqId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_RfqId_Status",
                table: "RfqInvitations",
                columns: new[] { "RfqId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_RfqId_VendorId_RoundNumber",
                table: "RfqInvitations",
                columns: new[] { "RfqId", "VendorId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_VendorId",
                table: "RfqInvitations",
                column: "VendorId");

            // 3) Backfill (RFQ-LIFECYCLE-ADDENDUM §5): one RfqInvitation per delimited vendor id.
            //    Status = BidSubmitted where a submitted bid exists for (RfqId, VendorId), else Invited.
            //    InvitedUtc = Rfq.CreatedUtc. The guid-shape guard skips any non-guid legacy token.
            migrationBuilder.Sql(@"
                INSERT INTO ""RfqInvitations"" (""Id"", ""RfqId"", ""VendorId"", ""RoundNumber"", ""Status"", ""InvitedUtc"")
                SELECT gen_random_uuid(), r.""Id"", v::uuid, 1,
                       CASE WHEN EXISTS (SELECT 1 FROM ""Bids"" b
                                          WHERE b.""RfqId"" = r.""Id"" AND b.""VendorId"" = v::uuid AND b.""Submitted"")
                            THEN 'BidSubmitted' ELSE 'Invited' END,
                       r.""CreatedUtc""
                FROM ""Rfqs"" r
                CROSS JOIN LATERAL unnest(string_to_array(r.""InvitedVendorIds"", '|')) AS v
                WHERE r.""InvitedVendorIds"" <> '' AND v ~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';");

            // OriginalClosesUtc = ClosesUtc for released (non-Draft) RFQs.
            migrationBuilder.Sql(@"UPDATE ""Rfqs"" SET ""OriginalClosesUtc"" = ""ClosesUtc"" WHERE ""Status"" <> 'Draft';");

            // 4) Drop the retired delimited column LAST (after the backfill has read it).
            migrationBuilder.DropColumn(
                name: "InvitedVendorIds",
                table: "Rfqs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the delimited column and repopulate from invitations (excluding Rescinded),
            // preserving the derived-list semantics, before dropping the new tables.
            migrationBuilder.AddColumn<string>(
                name: "InvitedVendorIds",
                table: "Rfqs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE ""Rfqs"" r SET ""InvitedVendorIds"" = COALESCE((
                    SELECT string_agg(i.""VendorId""::text, '|' ORDER BY i.""InvitedUtc"", i.""VendorId"")
                    FROM ""RfqInvitations"" i
                    WHERE i.""RfqId"" = r.""Id"" AND i.""Status"" <> 'Rescinded'), '');");

            migrationBuilder.DropTable(name: "RfqEvents");
            migrationBuilder.DropTable(name: "RfqInvitations");

            migrationBuilder.DropColumn(name: "OriginalClosesUtc", table: "Rfqs");
            migrationBuilder.DropColumn(name: "RoundNumber", table: "Rfqs");
            migrationBuilder.DropColumn(name: "WithdrawnUtc", table: "Bids");
        }
    }
}
