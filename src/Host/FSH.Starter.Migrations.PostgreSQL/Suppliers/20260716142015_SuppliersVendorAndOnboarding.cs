using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Suppliers
{
    /// <inheritdoc />
    public partial class SuppliersVendorAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "OnboardingApplicationCodeSeq",
                schema: "suppliers");

            migrationBuilder.CreateSequence(
                name: "VendorCodeSeq",
                schema: "suppliers",
                startValue: 10001L);

            migrationBuilder.CreateSequence(
                name: "VendorUserCodeSeq",
                schema: "suppliers");

            migrationBuilder.AddColumn<string>(
                name: "Categories",
                schema: "suppliers",
                table: "Vendors",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LlrcTier",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerms",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNo",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "State",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                schema: "suppliers",
                table: "Vendors",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SwecCategories",
                schema: "suppliers",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ParentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    IsLeaf = table.Column<bool>(type: "boolean", nullable: false),
                    PathText = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwecCategories", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "VendorAddresses",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorAddresses_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorBankAccounts",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Bank = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AccountNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Swift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorBankAccounts_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorCertifications",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ValidTo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorCertifications_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorContacts",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorContacts_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorCurrencies",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCurrencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorCurrencies_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorOnboardingApplications",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegisteredName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RegistrationNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TaxId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Categories = table.Column<string>(type: "text", nullable: false),
                    SelectedTemplateIds = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromotedVendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOnboardingApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorOnboardingInvitations",
                schema: "suppliers",
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
                    RevokedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOnboardingInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorUsers",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorUsers_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "suppliers",
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingAddresses",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Line = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Postcode = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingAddresses_VendorOnboardingApplications_Applicatio~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingAnswers",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FormTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingAnswers_VendorOnboardingApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingBankAccounts",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Bank = table.Column<string>(type: "text", nullable: false),
                    AccountNo = table.Column<string>(type: "text", nullable: false),
                    Swift = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingBankAccounts_VendorOnboardingApplications_Applica~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingCertifications",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    ValidTo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingCertifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingCertifications_VendorOnboardingApplications_Appli~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingClarificationRounds",
                schema: "suppliers",
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
                    RespondedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingClarificationRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingClarificationRounds_VendorOnboardingApplications_~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingContacts",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingContacts_VendorOnboardingApplications_Application~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingDocuments",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingDocuments_VendorOnboardingApplications_Applicatio~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingReviewSteps",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresFinance = table.Column<bool>(type: "boolean", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    DecidedByName = table.Column<string>(type: "text", nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingReviewSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingReviewSteps_VendorOnboardingApplications_Applicat~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorFinancialAssessments",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorFinancialAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorFinancialAssessments_VendorOnboardingApplications_App~",
                        column: x => x.ApplicationId,
                        principalSchema: "suppliers",
                        principalTable: "VendorOnboardingApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingClarificationItems",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Request = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingClarificationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingClarificationItems_OnboardingClarificationRounds_~",
                        column: x => x.RoundId,
                        principalSchema: "suppliers",
                        principalTable: "OnboardingClarificationRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingFinancialSnapshots",
                schema: "suppliers",
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
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingFinancialSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingFinancialSnapshots_VendorFinancialAssessments_Ass~",
                        column: x => x.AssessmentId,
                        principalSchema: "suppliers",
                        principalTable: "VendorFinancialAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingFinancialYears",
                schema: "suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    YearIndex = table.Column<int>(type: "integer", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    NetProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    Ebit = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalAssets = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentAssets = table.Column<decimal>(type: "numeric", nullable: false),
                    Inventory = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentLiabilities = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "numeric", nullable: false),
                    Equity = table.Column<decimal>(type: "numeric", nullable: false),
                    RetainedEarnings = table.Column<decimal>(type: "numeric", nullable: false),
                    FixedAssets = table.Column<decimal>(type: "numeric", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingFinancialYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingFinancialYears_VendorFinancialAssessments_Assessm~",
                        column: x => x.AssessmentId,
                        principalSchema: "suppliers",
                        principalTable: "VendorFinancialAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingAddresses_ApplicationId",
                schema: "suppliers",
                table: "OnboardingAddresses",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingAnswers_ApplicationId",
                schema: "suppliers",
                table: "OnboardingAnswers",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingAnswers_FormTemplateId",
                schema: "suppliers",
                table: "OnboardingAnswers",
                column: "FormTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingBankAccounts_ApplicationId",
                schema: "suppliers",
                table: "OnboardingBankAccounts",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingCertifications_ApplicationId",
                schema: "suppliers",
                table: "OnboardingCertifications",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationItems_RoundId",
                schema: "suppliers",
                table: "OnboardingClarificationItems",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationRounds_ApplicationId",
                schema: "suppliers",
                table: "OnboardingClarificationRounds",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingClarificationRounds_ApplicationId_RoundNo",
                schema: "suppliers",
                table: "OnboardingClarificationRounds",
                columns: new[] { "ApplicationId", "RoundNo" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingContacts_ApplicationId",
                schema: "suppliers",
                table: "OnboardingContacts",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDocuments_ApplicationId",
                schema: "suppliers",
                table: "OnboardingDocuments",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingFinancialSnapshots_AssessmentId",
                schema: "suppliers",
                table: "OnboardingFinancialSnapshots",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingFinancialYears_AssessmentId",
                schema: "suppliers",
                table: "OnboardingFinancialYears",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingFinancialYears_YearIndex",
                schema: "suppliers",
                table: "OnboardingFinancialYears",
                column: "YearIndex");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingReviewSteps_ApplicationId",
                schema: "suppliers",
                table: "OnboardingReviewSteps",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingReviewSteps_DecidedByUserId",
                schema: "suppliers",
                table: "OnboardingReviewSteps",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SwecCategories_ParentCode",
                schema: "suppliers",
                table: "SwecCategories",
                column: "ParentCode");

            migrationBuilder.CreateIndex(
                name: "IX_VendorAddresses_VendorId",
                schema: "suppliers",
                table: "VendorAddresses",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorBankAccounts_VendorId",
                schema: "suppliers",
                table: "VendorBankAccounts",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCertifications_VendorId",
                schema: "suppliers",
                table: "VendorCertifications",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorContacts_VendorId",
                schema: "suppliers",
                table: "VendorContacts",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCurrencies_VendorId",
                schema: "suppliers",
                table: "VendorCurrencies",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorFinancialAssessments_ApplicationId",
                schema: "suppliers",
                table: "VendorFinancialAssessments",
                columns: new[] { "ApplicationId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorFinancialAssessments_ApplicationId1",
                schema: "suppliers",
                table: "VendorFinancialAssessments",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Code",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_CreatedUtc",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_DecisionUtc",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "DecisionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_InvitationId",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Source",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Status",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_SubmittedUtc",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "SubmittedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingApplications_Type",
                schema: "suppliers",
                table: "VendorOnboardingApplications",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_ApplicationId",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_CreatedUtc",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_InvitedByUserId",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_Status",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorOnboardingInvitations_TokenHash",
                schema: "suppliers",
                table: "VendorOnboardingInvitations",
                columns: new[] { "TokenHash", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorUsers_Code",
                schema: "suppliers",
                table: "VendorUsers",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorUsers_Email",
                schema: "suppliers",
                table: "VendorUsers",
                columns: new[] { "Email", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorUsers_VendorId",
                schema: "suppliers",
                table: "VendorUsers",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingAddresses",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingAnswers",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingBankAccounts",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingCertifications",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingClarificationItems",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingContacts",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingDocuments",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingFinancialSnapshots",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingFinancialYears",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingReviewSteps",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "SwecCategories",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorAddresses",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorBankAccounts",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorCertifications",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorContacts",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorCurrencies",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorOnboardingInvitations",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorUsers",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "OnboardingClarificationRounds",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorFinancialAssessments",
                schema: "suppliers");

            migrationBuilder.DropTable(
                name: "VendorOnboardingApplications",
                schema: "suppliers");

            migrationBuilder.DropColumn(
                name: "Categories",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "City",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "LlrcTier",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "PaymentTerms",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "Region",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "RegistrationNo",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "TaxId",
                schema: "suppliers",
                table: "Vendors");

            migrationBuilder.DropSequence(
                name: "OnboardingApplicationCodeSeq",
                schema: "suppliers");

            migrationBuilder.DropSequence(
                name: "VendorCodeSeq",
                schema: "suppliers");

            migrationBuilder.DropSequence(
                name: "VendorUserCodeSeq",
                schema: "suppliers");
        }
    }
}
