using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Sourcing
{
    /// <inheritdoc />
    public partial class SourcingBidEvaluationAwardClarifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "AwardCodeSeq",
                schema: "sourcing");

            migrationBuilder.CreateSequence(
                name: "BidCodeSeq",
                schema: "sourcing");

            migrationBuilder.AddColumn<string>(
                name: "CommercialEvaluatorIds",
                schema: "sourcing",
                table: "Rfqs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CommercialOpened",
                schema: "sourcing",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TechFinalized",
                schema: "sourcing",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalEvaluatorIds",
                schema: "sourcing",
                table: "Rfqs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "TechnicalOpened",
                schema: "sourcing",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Awards",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ApproverUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ApprovedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Awards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Awards_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Submitted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SavedDraft = table.Column<bool>(type: "boolean", nullable: false),
                    WithdrawnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Lead = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Warranty = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clarifications",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SenderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadByBuyer = table.Column<bool>(type: "boolean", nullable: false),
                    ReadByVendor = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clarifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalScores",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluatorId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Criterion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalScores_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalSchema: "sourcing",
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwardAllocations",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqLineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AwardId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwardAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwardAllocations_Awards_AwardId",
                        column: x => x.AwardId,
                        principalSchema: "sourcing",
                        principalTable: "Awards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BidAnswers",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BidId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidAnswers_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "sourcing",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BidAttachments",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BidId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidAttachments_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "sourcing",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BidLines",
                schema: "sourcing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Bidding = table.Column<bool>(type: "boolean", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Partial = table.Column<bool>(type: "boolean", nullable: false),
                    AltItem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BidId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BidLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BidLines_Bids_BidId",
                        column: x => x.BidId,
                        principalSchema: "sourcing",
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwardAllocations_AwardId",
                schema: "sourcing",
                table: "AwardAllocations",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_Awards_Code",
                schema: "sourcing",
                table: "Awards",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Awards_RfqId",
                schema: "sourcing",
                table: "Awards",
                columns: new[] { "RfqId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BidAnswers_BidId",
                schema: "sourcing",
                table: "BidAnswers",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidAttachments_BidId",
                schema: "sourcing",
                table: "BidAttachments",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_BidLines_BidId",
                schema: "sourcing",
                table: "BidLines",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_Code",
                schema: "sourcing",
                table: "Bids",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bids_RfqId_VendorId",
                schema: "sourcing",
                table: "Bids",
                columns: new[] { "RfqId", "VendorId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clarifications_Scope_VendorId",
                schema: "sourcing",
                table: "Clarifications",
                columns: new[] { "Scope", "VendorId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalScores_RfqId_VendorId_EvaluatorId_Criterion",
                schema: "sourcing",
                table: "TechnicalScores",
                columns: new[] { "RfqId", "VendorId", "EvaluatorId", "Criterion", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwardAllocations",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "BidAnswers",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "BidAttachments",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "BidLines",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "Clarifications",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "TechnicalScores",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "Awards",
                schema: "sourcing");

            migrationBuilder.DropTable(
                name: "Bids",
                schema: "sourcing");

            migrationBuilder.DropColumn(
                name: "CommercialEvaluatorIds",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "CommercialOpened",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "TechFinalized",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "TechnicalEvaluatorIds",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "TechnicalOpened",
                schema: "sourcing",
                table: "Rfqs");

            migrationBuilder.DropSequence(
                name: "AwardCodeSeq",
                schema: "sourcing");

            migrationBuilder.DropSequence(
                name: "BidCodeSeq",
                schema: "sourcing");
        }
    }
}
