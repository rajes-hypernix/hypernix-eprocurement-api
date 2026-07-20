using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class InitialSourcing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sourcing");

            migrationBuilder.CreateSequence(
                name: "PrCodeSeq",
                schema: "sourcing");

            migrationBuilder.CreateSequence(
                name: "RfqCodeSeq",
                schema: "sourcing");

            migrationBuilder.CreateTable(
                name: "PrLineSourcings",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqLineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    QtySourced = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LinkStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrLineSourcings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequisitions",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Requestor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LocationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Job = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Memo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CostCentre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Project = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntryFormId = table.Column<Guid>(type: "uuid", nullable: true),
                    RaisedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    RequiredOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Submitted = table.Column<bool>(type: "boolean", nullable: false),
                    HeaderStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequisitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rfqs",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Envelope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpensUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtensionCount = table.Column<int>(type: "integer", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    PrRefs = table.Column<string>(type: "text", nullable: false),
                    TechnicalSections = table.Column<string>(type: "text", nullable: false),
                    CommercialSections = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rfqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrLines",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseRequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EstUnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrLines_PurchaseRequisitions_PurchaseRequisitionId",
                        column: x => x.PurchaseRequisitionId,
                        principalSchema: "sourcing",
                        principalTable: "PurchaseRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RfqEvents",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActorVendorUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReasonNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OldClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewClosesUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqEvents_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RfqFormItems",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Group = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Section = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: true),
                    Help = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqFormItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqFormItems_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RfqInvitations",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclineReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DeclineNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RescindReasonCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RescindNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InvitedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ViewedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RescindedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqInvitations_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RfqLines",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrRef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourcePrLineIds = table.Column<string>(type: "text", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfqLines_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_LifecycleStatus",
                schema: "sourcing",
                table: "PrLines",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PrLines_PurchaseRequisitionId",
                schema: "sourcing",
                table: "PrLines",
                column: "PurchaseRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_LinkStatus",
                schema: "sourcing",
                table: "PrLineSourcings",
                column: "LinkStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_PrLineId",
                schema: "sourcing",
                table: "PrLineSourcings",
                column: "PrLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PrLineSourcings_RfqId",
                schema: "sourcing",
                table: "PrLineSourcings",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_Code",
                schema: "sourcing",
                table: "PurchaseRequisitions",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequisitions_HeaderStatus",
                schema: "sourcing",
                table: "PurchaseRequisitions",
                column: "HeaderStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RfqEvents_RfqId_OccurredUtc",
                schema: "sourcing",
                table: "RfqEvents",
                columns: new[] { "RfqId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RfqFormItems_RfqId",
                schema: "sourcing",
                table: "RfqFormItems",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_RfqId_VendorId",
                schema: "sourcing",
                table: "RfqInvitations",
                columns: new[] { "RfqId", "VendorId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_Status",
                schema: "sourcing",
                table: "RfqInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RfqInvitations_VendorId",
                schema: "sourcing",
                table: "RfqInvitations",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLines_LineCode",
                schema: "sourcing",
                table: "RfqLines",
                column: "LineCode");

            migrationBuilder.CreateIndex(
                name: "IX_RfqLines_RfqId",
                schema: "sourcing",
                table: "RfqLines",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_Code",
                schema: "sourcing",
                table: "Rfqs",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_Status",
                schema: "sourcing",
                table: "Rfqs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrLines",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "PrLineSourcings",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "RfqEvents",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "RfqFormItems",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "RfqInvitations",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "RfqLines",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "PurchaseRequisitions",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "Rfqs",
                schema: "sourcing");

            migrationBuilder.DropSequence(
                name: "PrCodeSeq",
                schema: "sourcing");

            migrationBuilder.DropSequence(
                name: "RfqCodeSeq",
                schema: "sourcing");
        }
    }
}
