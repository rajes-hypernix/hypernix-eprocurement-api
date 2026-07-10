using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace eProcure.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SliceAVendorOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "FormTemplates",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Rfq");   // existing templates keep RFQ behaviour (additive, SPEC §5)

            migrationBuilder.CreateTable(
                name: "VendorOnboardingApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegisteredName = table.Column<string>(type: "text", nullable: false),
                    RegistrationNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TaxId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "text", nullable: false),
                    ContactPhone = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Categories = table.Column<string>(type: "text", nullable: false),
                    SelectedTemplateIds = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromotedVendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOnboardingApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorOnboardingInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SelectedTemplateIds = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvitedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvitedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOnboardingInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingAddresses",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Line = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Postcode = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingAddresses", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingAddresses_VendorOnboardingApplications_VendorOnbo~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingAnswers",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingAnswers", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingAnswers_VendorOnboardingApplications_VendorOnboar~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingBankAccounts",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Bank = table.Column<string>(type: "text", nullable: false),
                    AccountNo = table.Column<string>(type: "text", nullable: false),
                    Swift = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingBankAccounts", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingBankAccounts_VendorOnboardingApplications_VendorO~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingCertifications",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    ValidTo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingCertifications", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingCertifications_VendorOnboardingApplications_Vendo~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingClarificationRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNo = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RaisedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RaisedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RaisedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingClarificationRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingClarificationRounds_VendorOnboardingApplications_~",
                        column: x => x.ApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingContacts",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingContacts", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingContacts_VendorOnboardingApplications_VendorOnboa~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingReviewSteps",
                columns: table => new
                {
                    VendorOnboardingApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresFinance = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    DecidedByName = table.Column<string>(type: "text", nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingReviewSteps", x => new { x.VendorOnboardingApplicationId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingReviewSteps_VendorOnboardingApplications_VendorOn~",
                        column: x => x.VendorOnboardingApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorFinancialAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorFinancialAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorFinancialAssessments_VendorOnboardingApplications_App~",
                        column: x => x.ApplicationId,
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingClarificationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Request = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OnboardingClarificationRoundId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingClarificationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingClarificationItems_OnboardingClarificationRounds_~",
                        column: x => x.OnboardingClarificationRoundId,
                        principalTable: "OnboardingClarificationRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingFinancialSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WeightedZ = table.Column<double>(type: "double precision", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Band = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Statement = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VendorFinancialAssessmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingFinancialSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingFinancialSnapshots_VendorFinancialAssessments_Ven~",
                        column: x => x.VendorFinancialAssessmentId,
                        principalTable: "VendorFinancialAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingFinancialYears",
                columns: table => new
                {
                    VendorFinancialAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YearIndex = table.Column<int>(type: "integer", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Ebit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalAssets = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrentAssets = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Inventory = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrentLiabilities = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Equity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RetainedEarnings = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    FixedAssets = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingFinancialYears", x => new { x.VendorFinancialAssessmentId, x.Id });
                    table.ForeignKey(
                        name: "FK_OnboardingFinancialYears_VendorFinancialAssessments_VendorF~",
                        column: x => x.VendorFinancialAssessmentId,
                        principalTable: "VendorFinancialAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Purpose",
                table: "FormTemplates",
                column: "Purpose");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingAnswers_FormTemplateId",
                table: "OnboardingAnswers",
                column: "FormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationItems_OnboardingClarificationRoundId",
                table: "OnboardingClarificationItems",
                column: "OnboardingClarificationRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationRounds_ApplicationId",
                table: "OnboardingClarificationRounds",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationRounds_ApplicationId_RoundNo",
                table: "OnboardingClarificationRounds",
                columns: new[] { "ApplicationId", "RoundNo" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingFinancialSnapshots_VendorFinancialAssessmentId",
                table: "OnboardingFinancialSnapshots",
                column: "VendorFinancialAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingFinancialYears_YearIndex",
                table: "OnboardingFinancialYears",
                column: "YearIndex");

            migrationBuilder.CreateIndex(
                name: "IX_VendorFinancialAssessments_ApplicationId",
                table: "VendorFinancialAssessments",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Code",
                table: "VendorOnboardingApplications",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_CreatedUtc",
                table: "VendorOnboardingApplications",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_DecisionUtc",
                table: "VendorOnboardingApplications",
                column: "DecisionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_InvitationId",
                table: "VendorOnboardingApplications",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Source",
                table: "VendorOnboardingApplications",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Status",
                table: "VendorOnboardingApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_SubmittedUtc",
                table: "VendorOnboardingApplications",
                column: "SubmittedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Type",
                table: "VendorOnboardingApplications",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_ApplicationId",
                table: "VendorOnboardingInvitations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_CreatedUtc",
                table: "VendorOnboardingInvitations",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_Status",
                table: "VendorOnboardingInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                table: "VendorOnboardingInvitations",
                column: "TokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingAddresses");

            migrationBuilder.DropTable(
                name: "OnboardingAnswers");

            migrationBuilder.DropTable(
                name: "OnboardingBankAccounts");

            migrationBuilder.DropTable(
                name: "OnboardingCertifications");

            migrationBuilder.DropTable(
                name: "OnboardingClarificationItems");

            migrationBuilder.DropTable(
                name: "OnboardingContacts");

            migrationBuilder.DropTable(
                name: "OnboardingFinancialSnapshots");

            migrationBuilder.DropTable(
                name: "OnboardingFinancialYears");

            migrationBuilder.DropTable(
                name: "OnboardingReviewSteps");

            migrationBuilder.DropTable(
                name: "VendorOnboardingInvitations");

            migrationBuilder.DropTable(
                name: "OnboardingClarificationRounds");

            migrationBuilder.DropTable(
                name: "VendorFinancialAssessments");

            migrationBuilder.DropTable(
                name: "VendorOnboardingApplications");

            migrationBuilder.DropIndex(
                name: "IX_FormTemplates_Purpose",
                table: "FormTemplates");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "FormTemplates");
        }
    }
}
