using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class PlatformOrgAndFormTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FormTemplates",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrgUnits",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "platform",
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FormTemplateQuestions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Help = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormTemplateQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FormTemplateQuestions_FormTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "platform",
                        principalTable: "FormTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplateQuestions_TemplateId_Order",
                schema: "platform",
                table: "FormTemplateQuestions",
                columns: new[] { "TemplateId", "Order", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormTemplates_Key",
                schema: "platform",
                table: "FormTemplates",
                columns: new[] { "Key", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentId",
                schema: "platform",
                table: "OrgUnits",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_Type_Code",
                schema: "platform",
                table: "OrgUnits",
                columns: new[] { "Type", "Code", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FormTemplateQuestions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "OrgUnits",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "FormTemplates",
                schema: "platform");
        }
    }
}
