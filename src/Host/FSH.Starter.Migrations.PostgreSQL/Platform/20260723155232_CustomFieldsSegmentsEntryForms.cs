using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FSH.Starter.Migrations.PostgreSQL.Platform
{
    /// <inheritdoc />
    public partial class CustomFieldsSegmentsEntryForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomFieldDefs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RefEntity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ListKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShowInList = table.Column<bool>(type: "boolean", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    HelpText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValueText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ValueNumber = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ValueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ValueDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValueBool = table.Column<bool>(type: "boolean", nullable: true),
                    ValueListCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ValueRefId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValueLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormDefs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormDefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormRoleMaps",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntryFormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormRoleMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SegmentAssignments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Dimension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefApplications",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefApplications_CustomFieldDefs_CustomFieldDefId",
                        column: x => x.CustomFieldDefId,
                        principalSchema: "platform",
                        principalTable: "CustomFieldDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormFields",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryFormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    RequiredOnForm = table.Column<bool>(type: "boolean", nullable: false),
                    FullWidth = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormFields_EntryFormDefs_EntryFormDefId",
                        column: x => x.EntryFormDefId,
                        principalSchema: "platform",
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntryFormGroups",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryFormDefId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntryFormGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntryFormGroups_EntryFormDefs_EntryFormDefId",
                        column: x => x.EntryFormDefId,
                        principalSchema: "platform",
                        principalTable: "EntryFormDefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefApplications_CustomFieldDefId_RecordType",
                schema: "platform",
                table: "CustomFieldDefApplications",
                columns: new[] { "CustomFieldDefId", "RecordType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_Code",
                schema: "platform",
                table: "CustomFieldDefs",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_CustomFieldDefId_RecordType_RecordId",
                schema: "platform",
                table: "CustomFieldValues",
                columns: new[] { "CustomFieldDefId", "RecordType", "RecordId", "TenantId" },
                unique: true,
                filter: "\"LineId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_CustomFieldDefId_RecordType_RecordId_Line~",
                schema: "platform",
                table: "CustomFieldValues",
                columns: new[] { "CustomFieldDefId", "RecordType", "RecordId", "LineId", "TenantId" },
                unique: true,
                filter: "\"LineId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_RecordType_RecordId",
                schema: "platform",
                table: "CustomFieldValues",
                columns: new[] { "RecordType", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormDefs_Code",
                schema: "platform",
                table: "EntryFormDefs",
                columns: new[] { "Code", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormFields_EntryFormDefId",
                schema: "platform",
                table: "EntryFormFields",
                column: "EntryFormDefId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormFields_GroupId",
                schema: "platform",
                table: "EntryFormFields",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormGroups_EntryFormDefId",
                schema: "platform",
                table: "EntryFormGroups",
                column: "EntryFormDefId");

            migrationBuilder.CreateIndex(
                name: "IX_EntryFormRoleMaps_RecordType_Role",
                schema: "platform",
                table: "EntryFormRoleMaps",
                columns: new[] { "RecordType", "Role", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentAssignments_RecordType_RecordId_Dimension",
                schema: "platform",
                table: "SegmentAssignments",
                columns: new[] { "RecordType", "RecordId", "Dimension", "TenantId" },
                unique: true,
                filter: "\"LineId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentAssignments_RecordType_RecordId_LineId_Dimension",
                schema: "platform",
                table: "SegmentAssignments",
                columns: new[] { "RecordType", "RecordId", "LineId", "Dimension", "TenantId" },
                unique: true,
                filter: "\"LineId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldDefApplications",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CustomFieldValues",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "EntryFormFields",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "EntryFormGroups",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "EntryFormRoleMaps",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "SegmentAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "CustomFieldDefs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "EntryFormDefs",
                schema: "platform");
        }
    }
}
